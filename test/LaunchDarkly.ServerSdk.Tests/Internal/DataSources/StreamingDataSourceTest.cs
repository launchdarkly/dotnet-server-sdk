using System;
using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.TestHelpers;
using LaunchDarkly.TestHelpers.HttpTest;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.MockResponses;
using static LaunchDarkly.Sdk.Server.TestHttpUtils;
using static LaunchDarkly.Sdk.Server.TestUtils;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class StreamingDataSourceTest : BaseTest
    {
        private static readonly TimeSpan BriefReconnectDelay = TimeSpan.FromMilliseconds(10);

        private readonly CapturingDataSourceUpdates _updateSink = new CapturingDataSourceUpdates();

        public StreamingDataSourceTest(ITestOutputHelper testOutput) : base(testOutput) { }

        private IDataSource MakeDataSource(Uri baseUri, Action<ConfigurationBuilder> modConfig = null)
        {
            var builder = BasicConfig()
                .DataSource(Components.StreamingDataSource().InitialReconnectDelay(BriefReconnectDelay))
                .ServiceEndpoints(Components.ServiceEndpoints().Streaming(baseUri));
            modConfig?.Invoke(builder);
            var config = builder.Build();
            return config.DataSourceFactory.CreateDataSource(ContextFrom(config), _updateSink);
        }

        private IDataSource MakeDataSourceWithDiagnostics(Uri baseUri, IDiagnosticStore diagnosticStore)
        {
            var context = BasicContext.WithDiagnosticStore(diagnosticStore);
            return new StreamingDataSource(context, _updateSink, baseUri, BriefReconnectDelay);
        }

        private void WithDataSourceAndServer(Handler responseHandler, Action<IDataSource, HttpServer, Task> action)
        {
            using (var server = HttpServer.Start(responseHandler))
            {
                using (var dataSource = MakeDataSource(server.Uri))
                {
                    var initTask = dataSource.Start();
                    action(dataSource, server, initTask);
                }
            }
        }

        [Theory]
        [InlineData("", "/all")]
        [InlineData("/basepath", "/basepath/all")]
        [InlineData("/basepath/", "/basepath/all")]
        public void StreamRequestHasCorrectUri(string baseUriExtraPath, string expectedPath)
        {
            using (var server = HttpServer.Start(StreamWithEmptyData))
            {
                var baseUri = new Uri(server.Uri.ToString().TrimEnd('/') + baseUriExtraPath);
                using (var dataSource = MakeDataSource(baseUri))
                {
                    dataSource.Start();
                    var req = server.Recorder.RequireRequest();
                    Assert.Equal(expectedPath, req.Path);
                    Assert.Equal("GET", req.Method);
                }
            }
        }

        [Fact]
        public void PutCausesDataToBeStoredAndDataSourceInitialized()
        {
            var data = new DataSetBuilder()
                .Flags(new FeatureFlagBuilder("flag1").Version(1).Build())
                .Segments(new SegmentBuilder("seg1").Version(1).Build())
                .Build();

            WithDataSourceAndServer(StreamWithInitialData(data), (dataSource, _, initTask) =>
            {
                var receivedData = _updateSink.Inits.ExpectValue();
                AssertHelpers.DataSetsEqual(data, receivedData);

                Assert.True(initTask.Wait(TimeSpan.FromSeconds(1)));
                Assert.False(initTask.IsFaulted);

                Assert.True(dataSource.Initialized);
            });
        }

        [Fact]
        public void DataSourceIsNotInitializedByDefault()
        {
            WithDataSourceAndServer(StreamThatStaysOpenWithNoEvents, (dataSource, _, initTask) =>
            {
                Assert.False(dataSource.Initialized);
                Assert.False(initTask.IsCompleted);
            });
        }

        [Fact]
        public void PatchUpdatesFeature()
        {
            var flag = new FeatureFlagBuilder("flag1").Version(1).Build();
            var patchEvent = PatchEvent("/flags/flag1", flag.ToJsonString());

            WithDataSourceAndServer(StreamWithEmptyInitialDataAndThen(patchEvent), (dataSource, s, t) =>
            {
                var receivedPatch = _updateSink.Upserts.ExpectValue();
                Assert.Equal(DataModel.Features, receivedPatch.Kind);
                Assert.Equal(flag.Key, receivedPatch.Key);
                AssertHelpers.DataItemsEqual(DataModel.Features, DescriptorOf(flag), receivedPatch.Item);
            });
        }

        [Fact]
        public void PatchUpdatesSegment()
        {
            var segment = new SegmentBuilder("segment1").Version(1).Build();
            var patchEvent = PatchEvent("/segments/segment1", segment.ToJsonString());

            WithDataSourceAndServer(StreamWithEmptyInitialDataAndThen(patchEvent), (dataSource, s, t) =>
            {
                var receivedPatch = _updateSink.Upserts.ExpectValue();
                Assert.Equal(DataModel.Segments, receivedPatch.Kind);
                Assert.Equal(segment.Key, receivedPatch.Key);
                AssertHelpers.DataItemsEqual(DataModel.Segments, DescriptorOf(segment), receivedPatch.Item);
            });
        }

        [Fact]
        public void DeleteDeletesFeature()
        {
            var deleteEvent = DeleteEvent("/flags/flag1", 2);

            WithDataSourceAndServer(StreamWithEmptyInitialDataAndThen(deleteEvent), (dataSource, s, t) =>
            {
                var receivedPatch = _updateSink.Upserts.ExpectValue();
                Assert.Equal(DataModel.Features, receivedPatch.Kind);
                Assert.Equal("flag1", receivedPatch.Key);
                Assert.Equal(2, receivedPatch.Item.Version);
                Assert.Null(receivedPatch.Item.Item);
            });
        }

        [Fact]
        public void DeleteDeletesSegment()
        {
            var deleteEvent = DeleteEvent("/segments/segment1", 2);

            WithDataSourceAndServer(StreamWithEmptyInitialDataAndThen(deleteEvent), (dataSource, s, t) =>
            {
                var receivedPatch = _updateSink.Upserts.ExpectValue();
                Assert.Equal(DataModel.Segments, receivedPatch.Kind);
                Assert.Equal("segment1", receivedPatch.Key);
                Assert.Equal(2, receivedPatch.Item.Version);
                Assert.Null(receivedPatch.Item.Item);
            });
        }

        private void DoTestAfterEmptyPut(Handler contentHandler, Action<HttpServer> action)
        {
            var useContentForFirstRequestOnly = Handlers.Sequential(
                StreamWithEmptyInitialDataAndThen(contentHandler),
                StreamThatStaysOpenWithNoEvents
                );
            WithDataSourceAndServer(useContentForFirstRequestOnly, (dataSource, server, initTask) =>
                {
                    _updateSink.Inits.ExpectValue();
                    server.Recorder.RequireRequest();

                    action(server);
                });
        }

        [Theory]
        [InlineData(408)]
        [InlineData(429)]
        [InlineData(500)]
        [InlineData(503)]
        [InlineData(ServerErrorCondition.FakeIOException)]
        public void VerifyRecoverableHttpError(int errorStatus)
        {
            var errorCondition = ServerErrorCondition.FromStatus(errorStatus);

            WithServerErrorCondition(errorCondition, StreamWithEmptyData, (uri, httpConfig, recorder) =>
            {
                using (var dataSource = MakeDataSource(uri,
                    c => c.DataSource(Components.StreamingDataSource().InitialReconnectDelay(TimeSpan.Zero))
                        .Http(httpConfig)))
                {
                    var initTask = dataSource.Start();

                    var status = _updateSink.StatusUpdates.ExpectValue();
                    errorCondition.VerifyDataSourceStatusError(status);

                    // We don't check here for a second status update to the Valid state, because that was
                    // done by DataSourceUpdatesImpl when Init was called - our test fixture doesn't do it.

                    _updateSink.Inits.ExpectValue();

                    recorder.RequireRequest();
                    recorder.RequireRequest();

                    Assert.True(initTask.Wait(TimeSpan.FromSeconds(1)));
                    Assert.True(dataSource.Initialized);
                }
                errorCondition.VerifyLogMessage(LogCapture);
            });
        }

        [Theory]
        [InlineData(401)]
        [InlineData(403)]
        public void VerifyUnrecoverableHttpError(int errorStatus)
        {
            var errorCondition = ServerErrorCondition.FromStatus(errorStatus);

            WithServerErrorCondition(errorCondition, StreamWithEmptyData, (uri, httpConfig, recorder) =>
            {
                using (var dataSource = MakeDataSource(uri,
                    c => c.DataSource(Components.StreamingDataSource().InitialReconnectDelay(TimeSpan.Zero))
                        .Http(httpConfig)))
                {
                    var initTask = dataSource.Start();
                    var status = _updateSink.StatusUpdates.ExpectValue();
                    errorCondition.VerifyDataSourceStatusError(status);

                    _updateSink.Inits.ExpectNoValue();

                    recorder.RequireRequest();
                    recorder.RequireNoRequests(TimeSpan.FromMilliseconds(100));

                    Assert.True(initTask.Wait(TimeSpan.FromSeconds(1)));
                    Assert.False(dataSource.Initialized);

                    errorCondition.VerifyLogMessage(LogCapture);
                }
            });
        }

        [Fact]
        public async void StreamInitDiagnosticRecordedOnOpen()
        {
            var receivedFailed = new EventSink<bool>();
            var mockDiagnosticStore = new Mock<IDiagnosticStore>();
            mockDiagnosticStore.Setup(m => m.AddStreamInit(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<bool>()))
                .Callback((DateTime timestamp, TimeSpan duration, bool failed) => receivedFailed.Enqueue(failed));

            using (var server = HttpServer.Start(StreamWithEmptyData))
            {
                using (var dataSource = MakeDataSourceWithDiagnostics(server.Uri, mockDiagnosticStore.Object))
                {
                    await dataSource.Start();

                    Assert.False(receivedFailed.ExpectValue());
                }
            }
        }

        [Fact]
        public async void StreamInitDiagnosticRecordedOnError()
        {
            var receivedFailed = new EventSink<bool>();
            var mockDiagnosticStore = new Mock<IDiagnosticStore>();
            mockDiagnosticStore.Setup(m => m.AddStreamInit(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<bool>()))
                .Callback((DateTime timestamp, TimeSpan duration, bool failed) => receivedFailed.Enqueue(failed));

            using (var server = HttpServer.Start(Handlers.Status(401)))
            {
                using (var dataSource = MakeDataSourceWithDiagnostics(server.Uri, mockDiagnosticStore.Object))
                {
                    await dataSource.Start();

                    Assert.True(receivedFailed.ExpectValue());
                }
            }
        }

        [Theory]
        [InlineData("put", "{sorry")]
        [InlineData("patch", "{sorry")]
        [InlineData("delete", "{sorry")]
        public void EventWithMalformedJsonCausesStreamRestart(string eventName, string data)
        {
            VerifyEventCausesStreamRestart(eventName, data,
                err => Assert.Equal(DataSourceStatus.ErrorKind.InvalidData, err.Kind));
        }

        [Theory]
        [InlineData("put", @"{""data"":{""flags"":3}}")]
        [InlineData("patch", @"{""path"":""/flags/flagkey"", ""data"":{""rules"":3}}")]
        [InlineData("patch", @"{""data"":{""version"":1}}")]
        [InlineData("delete", @"{""version"":1}")]
        public void EventWithWellFormedJsonButInvalidDataCausesStreamRestart(string eventName, string data)
        {
            VerifyEventCausesStreamRestart(eventName, data,
                err => Assert.Equal(DataSourceStatus.ErrorKind.InvalidData, err.Kind));
        }

        [Theory]
        [InlineData("patch", @"{""path"":""/things/1"", ""data"":{""version"":1}}")]
        [InlineData("delete", @"{""path"":""/things/2"", ""version"":1}")]
        public void PatchOrDeleteEventWithUnrecognizedPathDoesNotCauseStreamRestart(string eventName, string data)
        {
            VerifyEventDoesNotCauseStreamRestart(eventName, data);
        }

        [Fact]
        public void UnknownEventTypeDoesNotCauseError()
        {
            VerifyEventDoesNotCauseStreamRestart("weird", "data");
        }

        [Fact]
        public void RestartsStreamIfStoreNeedsRefresh()
        {
            _updateSink.MockDataStoreStatusProvider.StatusMonitoringEnabled = true;

            DoTestAfterEmptyPut(null, server =>
            {
                _updateSink.MockDataStoreStatusProvider.FireStatusChanged(
                    new DataStoreStatus { Available = false, RefreshNeeded = false });
                _updateSink.MockDataStoreStatusProvider.FireStatusChanged(
                    new DataStoreStatus { Available = true, RefreshNeeded = true });

                server.Recorder.RequireRequest();

                AssertLogMessage(true, LogLevel.Warn, "Restarting stream to refresh data after data store outage");
            });
        }

        [Fact]
        public void DoesNotRestartStreamIfStoreHadOutageButDoesNotNeedRefresh()
        {
            _updateSink.MockDataStoreStatusProvider.StatusMonitoringEnabled = true;

            DoTestAfterEmptyPut(null, server =>
            {
                _updateSink.MockDataStoreStatusProvider.FireStatusChanged(
                    new DataStoreStatus { Available = false, RefreshNeeded = false });
                _updateSink.MockDataStoreStatusProvider.FireStatusChanged(
                    new DataStoreStatus { Available = true, RefreshNeeded = false });

                server.Recorder.RequireNoRequests(TimeSpan.FromMilliseconds(100));

                AssertLogMessage(false, LogLevel.Warn, "Restarting stream to refresh data after data store outage");
            });
        }

        [Fact]
        public void StoreFailureOnPutCausesStreamRestartWhenStatusMonitoringIsNotAvailable()
        {
            // If StatusMonitoringEnabled is false, it means we're using either an in-memory store or some kind
            // of custom implementation that doesn't support our usual "wait till the database is up again and
            // then re-request the updates if necessary" logic. That's an unlikely case (the in-memory store
            // should never throw an exception) but the expected behavior is that the stream gets immediately
            // restarted.

            _updateSink.MockDataStoreStatusProvider.StatusMonitoringEnabled = false;
            _updateSink.InitsShouldFail = 1;

            DoTestAfterEmptyPut(null, server =>
            {
                server.Recorder.RequireRequest();

                // We won't check _updateSink.StatusUpdates here, because the behavior of sending an error update
                // (with a Kind of StoreError) is provided by DataSourceUpdatesImpl, not by StreamProcessor, and
                // we're not using a real DataSourceUpdatesImpl.

                AssertLogMessage(true, LogLevel.Warn, "Restarting stream to ensure that we have the latest data");
            });
        }

        [Fact]
        public void StoreFailureOnPatchCausesStreamRestartWhenStatusMonitoringIsNotAvailable()
        {
            _updateSink.MockDataStoreStatusProvider.StatusMonitoringEnabled = false;
            _updateSink.UpsertsShouldFail = 1;
            var patchEvent = PatchEvent("/flags/flag1", new FeatureFlagBuilder("flag1").Build().ToJsonString());

            DoTestAfterEmptyPut(patchEvent, server =>
            {
                server.Recorder.RequireRequest();

                // See comment in StoreFailureOnPutCausesStreamRestartWhenStatusMonitoringIsNotAvailable about status

                AssertLogMessage(true, LogLevel.Warn, "Restarting stream to ensure that we have the latest data");
            });
        }

        [Fact]
        public void StoreFailureOnDeleteCausesStreamRestartWhenStatusMonitoringIsNotAvailable()
        {
            _updateSink.MockDataStoreStatusProvider.StatusMonitoringEnabled = false;
            _updateSink.UpsertsShouldFail = 1;
            var deleteEvent = DeleteEvent("/flags/flag1", 1);

            DoTestAfterEmptyPut(deleteEvent, server =>
            {
                server.Recorder.RequireRequest();

                // See comment in StoreFailureOnPutCausesStreamRestartWhenStatusMonitoringIsNotAvailable about status

                AssertLogMessage(true, LogLevel.Warn, "Restarting stream to ensure that we have the latest data");
            });
        }

        private void VerifyEventCausesStreamRestart(string eventName, string eventData,
            Action<DataSourceStatus.ErrorInfo> verifyError)
        {
            DoTestAfterEmptyPut(Handlers.SSE.Event(eventName, eventData), server =>
            {
                server.Recorder.RequireRequest();

                // We did not allow the stream to successfully process an event before causing the error, so the
                // state will still be Initializing, but we should be able to see that an error happened.
                var status = _updateSink.StatusUpdates.ExpectValue();
                Assert.NotNull(status.LastError);
                verifyError?.Invoke(status.LastError.Value);
            });
        }

        private void VerifyEventDoesNotCauseStreamRestart(string eventName, string eventData)
        {
            // We'll end another event after that event, so we can see when we've got past the first one
            var events = Handlers.SSE.Event(eventName, eventData)
                .Then(PatchEvent("/flags/ignore", new FeatureFlagBuilder("flag1").Build().ToJsonString()));

            DoTestAfterEmptyPut(events, server =>
            {
                var receivedPatch = _updateSink.Upserts.ExpectValue();
                Assert.Equal("ignore", receivedPatch.Key);

                server.Recorder.RequireNoRequests(TimeSpan.FromMilliseconds(100));
                _updateSink.StatusUpdates.ExpectNoValue();

                Assert.Empty(LogCapture.GetMessages().Where(m => m.Level == Logging.LogLevel.Error));
            });
        }
    }
}
