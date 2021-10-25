using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Json;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.TestHelpers;
using LaunchDarkly.EventSource;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class StreamProcessorTest : BaseTest
    {
        private const string FEATURE_KEY = "feature";
        private const int FEATURE_VERSION = 11;
        private static readonly FeatureFlag FEATURE = new FeatureFlagBuilder(FEATURE_KEY).Version(FEATURE_VERSION).On(true).Build();
        private const string SEGMENT_KEY = "segment";
        private const int SEGMENT_VERSION = 22;
        private static readonly Segment SEGMENT = new SegmentBuilder(SEGMENT_KEY).Version(SEGMENT_VERSION).Build();
        private const string EmptyPutData = "{\"data\":{\"flags\":{},\"segments\":{}}}";

        readonly Mock<IEventSource> _mockEventSource;
        readonly IEventSource _eventSource;
        readonly TestEventSourceFactory _eventSourceFactory;
        readonly DelegatingDataStoreForStreamTests _dataStore;
        readonly DataStoreUpdatesImpl _dataStoreUpdates;
        readonly IDataStoreStatusProvider _dataStoreStatusProvider;
        readonly DataSourceUpdatesImpl _dataSourceUpdates;
        readonly IDataSourceStatusProvider _dataSourceStatusProvider;
        Configuration _config;

        EventWaitHandle _esStartedReady = new EventWaitHandle(false, EventResetMode.AutoReset);

        public StreamProcessorTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            _mockEventSource = new Mock<IEventSource>();
            _mockEventSource.Setup(es => es.StartAsync()).Returns(TestUtils.CompletedTask()).Callback(() => _esStartedReady.Set());
            _eventSource = _mockEventSource.Object;
            _eventSourceFactory = new TestEventSourceFactory(_eventSource);
            _dataStore = new DelegatingDataStoreForStreamTests { WrappedStore = new InMemoryDataStore() };
            _dataStoreUpdates = new DataStoreUpdatesImpl(BasicTaskExecutor, TestLogger);
            _dataStoreStatusProvider = new DataStoreStatusProviderImpl(_dataStore, _dataStoreUpdates);
            _dataSourceUpdates = new DataSourceUpdatesImpl(
                _dataStore,
                _dataStoreStatusProvider,
                BasicTaskExecutor,
                TestLogger,
                null
                );
            _dataSourceStatusProvider = new DataSourceStatusProviderImpl(_dataSourceUpdates);
            _config = BasicConfig()
                .DataSource(Components.StreamingDataSource().EventSourceCreator(_eventSourceFactory.Create()))
                .DataStore(_dataStore.AsSingletonFactory())
                .Build();
        }

        [Theory]
        [InlineData("", "/all")]
        [InlineData("/basepath", "/basepath/all")]
        [InlineData("/basepath/", "/basepath/all")]
        public void StreamRequestHasCorrectUri(string baseUriExtraPath, string expectedPath)
        {
            var baseUri = new Uri("http://stream.test.com" + baseUriExtraPath);
            _config = Configuration.Builder(_config)
                .DataSource(
                    Components.StreamingDataSource()
                        .EventSourceCreator(_eventSourceFactory.Create())
                    )
                .ServiceEndpoints(Components.ServiceEndpoints().Streaming(baseUri))
                .Build();
            StreamProcessor sp = CreateAndStartProcessor();
            Assert.Equal(new Uri("http://stream.test.com" + expectedPath),
                _eventSourceFactory.ReceivedUri);
        }

        [Fact]
        public void PutCausesFeatureToBeStored()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            string data = LdValue.BuildObject()
                .Add("data", LdValue.BuildObject()
                    .Add("flags", LdValue.BuildObject()
                        .Add(FEATURE_KEY, LdValue.Parse(LdJsonSerialization.SerializeObject(FEATURE)))
                        .Build())
                    .Add("segments", LdValue.BuildObject().Build())
                    .Build())
                .Build().ToJsonString();
            SimulateMessageReceived("put", data);

            AssertFeatureInStore(FEATURE);
        }

        [Fact]
        public void PutCausesSegmentToBeStored()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            string data = LdValue.BuildObject()
                .Add("data", LdValue.BuildObject()
                    .Add("flags", LdValue.BuildObject().Build())
                    .Add("segments", LdValue.BuildObject()
                        .Add(SEGMENT_KEY, LdValue.Parse(LdJsonSerialization.SerializeObject(SEGMENT)))
                        .Build())
                    .Build())
                .Build().ToJsonString();
            SimulateMessageReceived("put", data);

            AssertSegmentInStore(SEGMENT);
        }

        [Fact]
        public void StoreNotInitializedByDefault()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            Assert.False(_dataStore.Initialized());
        }

        [Fact]
        public void PutCausesStoreToBeInitialized()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            SimulateMessageReceived("put", EmptyPutData);
            Assert.True(_dataStore.Initialized());
        }

        [Fact]
        public void ProcessorNotInitializedByDefault()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            Assert.False(sp.Initialized);
            Assert.Equal(DataSourceState.Initializing, _dataSourceStatusProvider.Status.State);
        }

        [Fact]
        public void PutCausesProcessorToBeInitialized()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            SimulateMessageReceived("put", EmptyPutData);
            Assert.True(sp.Initialized);
            Assert.Equal(DataSourceState.Valid, _dataSourceStatusProvider.Status.State);
        }

        [Fact]
        public void TaskIsNotCompletedByDefault()
        {
            StreamProcessor sp = CreateProcessor();
            Task<bool> task = sp.Start();
            Assert.False(task.IsCompleted);
        }

        [Fact]
        public void PutCausesTaskToBeCompleted()
        {
            StreamProcessor sp = CreateProcessor();
            Task<bool> task = sp.Start();
            SimulateMessageReceived("put", EmptyPutData);
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void PatchUpdatesFeature()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            SimulateMessageReceived("put", EmptyPutData);

            string path = "/flags/" + FEATURE_KEY;
            string data = LdValue.BuildObject()
                .Add("path", path)
                .Add("data", LdValue.Parse(LdJsonSerialization.SerializeObject(FEATURE)))
                .Build().ToJsonString();
            SimulateMessageReceived("patch", data);

            AssertFeatureInStore(FEATURE);
        }

        [Fact]
        public void PatchUpdatesSegment()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            SimulateMessageReceived("put", EmptyPutData);

            string path = "/segments/" + SEGMENT_KEY;
            string data = LdValue.BuildObject()
                .Add("path", path)
                .Add("data", LdValue.Parse(LdJsonSerialization.SerializeObject(SEGMENT)))
                .Build().ToJsonString();
            SimulateMessageReceived("patch", data);

            AssertSegmentInStore(SEGMENT);
        }

        [Fact]
        public void DeleteDeletesFeature()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            SimulateMessageReceived("put", EmptyPutData);
            TestUtils.UpsertFlag(_dataStore, FEATURE);

            string path = "/flags/" + FEATURE_KEY;
            int deletedVersion = FEATURE.Version + 1;
            string data = LdValue.BuildObject()
                .Add("path", path)
                .Add("version", deletedVersion)
                .Build().ToJsonString();
            SimulateMessageReceived("delete", data);

            Assert.Equal(ItemDescriptor.Deleted(deletedVersion),
                _dataStore.Get(DataModel.Features, FEATURE_KEY));
        }

        [Fact]
        public void DeleteDeletesSegment()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            SimulateMessageReceived("put", EmptyPutData);
            TestUtils.UpsertSegment(_dataStore, SEGMENT);

            string path = "/segments/" + SEGMENT_KEY;
            int deletedVersion = SEGMENT.Version + 1;
            string data = LdValue.BuildObject()
                .Add("path", path)
                .Add("version", deletedVersion)
                .Build().ToJsonString();
            SimulateMessageReceived("delete", data);

            Assert.Equal(ItemDescriptor.Deleted(deletedVersion),
                _dataStore.Get(DataModel.Segments, SEGMENT_KEY));
        }

        [Fact]
        public void RecoverableErrorChangesStateToInterrupted()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            SimulateMessageReceived("put", EmptyPutData);
            Assert.True(sp.Initialized);
            Assert.Equal(DataSourceState.Valid, _dataSourceStatusProvider.Status.State);

            SimulateStreamHttpError(500);

            var newStatus = _dataSourceStatusProvider.Status;
            Assert.Equal(DataSourceState.Interrupted, newStatus.State);
            Assert.NotNull(newStatus.LastError);
            Assert.Equal(DataSourceStatus.ErrorKind.ErrorResponse, newStatus.LastError.Value.Kind);
            Assert.Equal(500, newStatus.LastError.Value.StatusCode);
        }

        [Fact]
        public void UnrecoverableErrorChangesStateToOff()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            SimulateMessageReceived("put", EmptyPutData);
            Assert.True(sp.Initialized);
            Assert.Equal(DataSourceState.Valid, _dataSourceStatusProvider.Status.State);

            SimulateStreamHttpError(401);

            var newStatus = _dataSourceStatusProvider.Status;
            Assert.Equal(DataSourceState.Off, newStatus.State);
            Assert.NotNull(newStatus.LastError);
            Assert.Equal(DataSourceStatus.ErrorKind.ErrorResponse, newStatus.LastError.Value.Kind);
            Assert.Equal(401, newStatus.LastError.Value.StatusCode);
        }

        [Fact]
        public void StreamInitDiagnosticRecordedOnOpen()
        {
            var mockDiagnosticStore = new Mock<IDiagnosticStore>();
            var diagnosticStore = mockDiagnosticStore.Object;
            var context = new LdClientContext(BasicContext.Basic,
                Components.HttpConfiguration().CreateHttpConfiguration(BasicContext.Basic),
                diagnosticStore, BasicTaskExecutor);

            using (var sp = (StreamProcessor)Components.StreamingDataSource().EventSourceCreator(_eventSourceFactory.Create())
                .CreateDataSource(context, _dataSourceUpdates))
            {
                sp.Start();
                Assert.True(_esStartedReady.WaitOne(TimeSpan.FromSeconds(1)));
                DateTime esStarted = sp._esStarted;
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                _mockEventSource.Raise(es => es.Opened += null, new EventSource.StateChangedEventArgs(ReadyState.Open));
                DateTime startCompleted = sp._esStarted;

                Assert.True(esStarted != startCompleted);
                mockDiagnosticStore.Verify(ds => ds.AddStreamInit(esStarted,
                    It.Is<TimeSpan>(ts => TimeSpan.Equals(ts, startCompleted - esStarted)), false));
            }
        }

        [Fact]
        public void StreamInitDiagnosticRecordedOnError()
        {
            var mockDiagnosticStore = new Mock<IDiagnosticStore>();
            var diagnosticStore = mockDiagnosticStore.Object;
            var context = new LdClientContext(BasicContext.Basic,
                Components.HttpConfiguration().CreateHttpConfiguration(BasicContext.Basic),
                diagnosticStore, BasicTaskExecutor);

            using (var sp = (StreamProcessor)Components.StreamingDataSource().EventSourceCreator(_eventSourceFactory.Create())
                .CreateDataSource(context, _dataSourceUpdates))
            {
                sp.Start();
                Assert.True(_esStartedReady.WaitOne(TimeSpan.FromSeconds(1)));
                DateTime esStarted = sp._esStarted;
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                SimulateStreamHttpError(401);
                DateTime startFailed = sp._esStarted;

                Assert.True(esStarted != startFailed);
                mockDiagnosticStore.Verify(ds => ds.AddStreamInit(esStarted, It.Is<TimeSpan>(ts => TimeSpan.Equals(ts, startFailed - esStarted)), true));
            }
        }

        [Fact]
        public void PutEventWithInvalidJsonCausesStreamRestart()
        {
            VerifyInvalidDataEvent("put", "{sorry");
        }

        [Fact]
        public void PutEventWithWellFormedJsonButInvalidDataCausesStreamRestart()
        {
            VerifyInvalidDataEvent("put", "{\"data\":{\"flags\":3}}");
        }

        [Fact]
        public void PatchEventWithInvalidJsonCausesStreamRestart()
        {
            VerifyInvalidDataEvent("patch", "{sorry");
        }

        [Fact]
        public void PatchEventWithWellFormedJsonButInvalidDataCausesStreamRestart()
        {
            VerifyInvalidDataEvent("patch", "{\"path\":\"/flags/flagkey\", \"data\":{\"rules\":3}}");
        }

        [Fact]
        public void PatchEventWithInvalidPathCausesNoStreamRestart()
        {
            VerifyEventCausesNoStreamRestart("patch", "{\"path\":\"/wrong\", \"data\":{\"key\":\"flagkey\"}}");
        }

        [Fact]
        public void DeleteEventWithInvalidJsonCausesStreamRestart()
        {
            VerifyInvalidDataEvent("delete", "{sorry");
        }

        [Fact]
        public void DeleteEventWithInvalidPathCausesNoStreamRestart()
        {
            VerifyEventCausesNoStreamRestart("delete", "{\"path\":\"/wrong\", \"version\":1}");
        }

        [Fact]
        public void RestartsStreamIfStoreNeedsRefresh()
        {
            var mockDataStore = new Mock<IDataStore>();
            _dataStore.WrappedStore = mockDataStore.Object;
            mockDataStore.Setup(d => d.StatusMonitoringEnabled).Returns(true);

            using (StreamProcessor sp = CreateAndStartProcessor())
            {
                ExpectStreamRestart(() =>
                {
                    _dataStoreUpdates.UpdateStatus(new DataStoreStatus { Available = false, RefreshNeeded = false });
                    _dataStoreUpdates.UpdateStatus(new DataStoreStatus { Available = true, RefreshNeeded = true });
                });
            }
        }

        [Fact]
        public void DoesNotRestartStreamIfStoreHadOutageButDoesNotNeedRefresh()
        {
            var mockDataStore = new Mock<IDataStore>();
            _dataStore.WrappedStore = mockDataStore.Object;
            mockDataStore.Setup(d => d.StatusMonitoringEnabled).Returns(true);

            using (StreamProcessor sp = CreateAndStartProcessor())
            {
                ExpectNoStreamRestart(() =>
                {
                    _dataStoreUpdates.UpdateStatus(new DataStoreStatus { Available = false, RefreshNeeded = false });
                    _dataStoreUpdates.UpdateStatus(new DataStoreStatus { Available = true, RefreshNeeded = false });

                    // No way to really synchronize on this condition, since no action is taken
                    Thread.Sleep(TimeSpan.FromMilliseconds(300));
                });
            }
        }

        [Fact]
        public void StoreFailureOnPutCausesStreamRestartWhenStatusMonitoringIsNotAvailable()
        {
            // If StatusMonitoringEnabled is false, it means we're using either an in-memory store or some kind
            // of custom implementation that doesn't support our usual "wait till the database is up again and
            // then re-request the updates if necessary" logic. That's an unlikely case but the expected behavior
            // is that the stream gets immediately restarted.
            var mockDataStore = new Mock<IDataStore>();
            _dataStore.WrappedStore = mockDataStore.Object;
            mockDataStore.Setup(d => d.StatusMonitoringEnabled).Returns(false);
            mockDataStore.Setup(d => d.Init(Moq.It.IsAny<FullDataSet<ItemDescriptor>>())).Throws(new Exception("sorry"));

            using (StreamProcessor sp = CreateAndStartProcessor())
            {
                ExpectStreamRestart(() =>
                {
                    SimulateMessageReceived("put", EmptyPutData);
                });
            }

            var status = _dataSourceStatusProvider.Status;
            Assert.NotNull(status.LastError);
            Assert.Equal(DataSourceStatus.ErrorKind.StoreError, status.LastError.Value.Kind);
        }

        [Fact]
        public void StoreFailureOnPatchCausesStreamRestartWhenStatusMonitoringIsNotAvailable()
        {
            var mockDataStore = new Mock<IDataStore>();
            _dataStore.WrappedStore = mockDataStore.Object;
            mockDataStore.Setup(d => d.StatusMonitoringEnabled).Returns(false);
            mockDataStore.Setup(d => d.Upsert(Moq.It.IsAny<DataKind>(), Moq.It.IsAny<string>(),
                Moq.It.IsAny<ItemDescriptor>())).Throws(new Exception("sorry"));

            using (StreamProcessor sp = CreateAndStartProcessor())
            {
                ExpectStreamRestart(() =>
                {
                    SimulateMessageReceived("patch",
                        "{\"path\":\"/flags/flagkey\",\"data\":{\"key\":\"flagkey\",\"version\":1}}");
                });
            }
        }

        [Fact]
        public void StoreFailureOnDeleteCausesStreamRestartWhenStatusMonitoringIsNotAvailable()
        {
            var mockDataStore = new Mock<IDataStore>();
            _dataStore.WrappedStore = mockDataStore.Object;
            mockDataStore.Setup(d => d.StatusMonitoringEnabled).Returns(false);
            mockDataStore.Setup(d => d.Upsert(Moq.It.IsAny<DataKind>(), Moq.It.IsAny<string>(),
                Moq.It.IsAny<ItemDescriptor>())).Throws(new Exception("sorry"));

            using (StreamProcessor sp = CreateAndStartProcessor())
            {
                ExpectStreamRestart(() =>
                {
                    SimulateMessageReceived("delete",
                        "{\"path\":\"/flags/flagkey\",\"version\":1}");
                });
            }
        }

        private StreamProcessor CreateProcessor()
        {
            var basicConfig = new BasicConfiguration(_config, TestLogger);
            return _config.DataSourceFactory.CreateDataSource(
                new LdClientContext(basicConfig, _config),
                _dataSourceUpdates
                ) as StreamProcessor;
        }

        private StreamProcessor CreateAndStartProcessor()
        {
            StreamProcessor sp = CreateProcessor();
            sp.Start();
            Assert.True(_esStartedReady.WaitOne(TimeSpan.FromSeconds(5)));
            _esStartedReady.Reset();
            return sp;
        }

        private void VerifyEventCausesNoStreamRestart(string eventName, string eventData)
        {
            using (StreamProcessor sp = CreateAndStartProcessor())
            {
                ExpectNoStreamRestart(() => SimulateMessageReceived(eventName, eventData));
            }
        }

        private void VerifyEventCausesStreamRestartWithInMemoryStore(string eventName, string eventData)
        {
            using (StreamProcessor sp = CreateAndStartProcessor())
            {
                ExpectStreamRestart(() => SimulateMessageReceived(eventName, eventData));
            }
        }

        private void VerifyInvalidDataEvent(string eventName, string eventData)
        {
            var statuses = new EventSink<DataSourceStatus>();
            _dataSourceStatusProvider.StatusChanged += statuses.Add;

            VerifyEventCausesStreamRestartWithInMemoryStore(eventName, eventData);

            // We did not allow the stream to successfully process an event before causing the error, so the
            // state will still be Initializing, but we should be able to see that an error happened.
            var status = statuses.ExpectValue();
            Assert.NotNull(status.LastError);
            Assert.Equal(DataSourceStatus.ErrorKind.InvalidData, status.LastError.Value.Kind);
        }

        private void ExpectNoStreamRestart(Action action)
        {
            _mockEventSource.ResetCalls();
            action();
            _mockEventSource.Verify(es => es.Restart(It.IsAny<bool>()), Times.Never);
            _mockEventSource.ResetCalls();
        }

        private void ExpectStreamRestart(Action action)
        {
            // The Restart call might happen asynchronously (for instance if it's triggered by a data
            // store status change), so we can't just call Verify after action(). Instead, we'll use a
            // callback to set a condition we can wait on.
            _mockEventSource.ResetCalls();
            var restarted = new EventWaitHandle(false, EventResetMode.AutoReset);
            _mockEventSource.Setup(es => es.Restart(false)).Callback(() => restarted.Set());
            action();
            Assert.True(restarted.WaitOne(TimeSpan.FromSeconds(5)), "timed out waiting for stream restart");
            _mockEventSource.ResetCalls();
        }

        private void SimulateMessageReceived(string eventName, string eventData)
        {
            var evt = new MessageReceivedEventArgs(new MessageEvent(eventName, eventData, null));
            _mockEventSource.Raise(es => es.MessageReceived += null, evt);
        }

        private void SimulateStreamHttpError(int statusCode)
        {
            var ex = new EventSourceServiceUnsuccessfulResponseException(statusCode);
            _mockEventSource.Raise(es => es.Error += null, new EventSource.ExceptionEventArgs(ex));
        }

        class TestEventSourceFactory
        {
            public Uri ReceivedUri { get; private set; }
            public IDictionary<string, string> ReceivedHeaders { get; private set; }
            readonly IEventSource _eventSource;

            public TestEventSourceFactory(IEventSource eventSource)
            {
                _eventSource = eventSource;
            }

            public StreamProcessor.EventSourceCreator Create()
            {
                return (Uri uri, HttpConfiguration httpConfig) =>
                {
                    ReceivedUri = uri;
                    ReceivedHeaders = httpConfig.DefaultHeaders.ToDictionary(kv => kv.Key, kv => kv.Value);
                    return _eventSource;
                };
            }
        }
        
        private void AssertFeatureInStore(FeatureFlag f)
        {
            Assert.Equal(f.Version, _dataStore.Get(DataModel.Features, f.Key).Value.Version);
        }

        private void AssertSegmentInStore(Segment s)
        {
            Assert.Equal(s.Version, _dataStore.Get(DataModel.Segments, s.Key).Value.Version);
        }
    }

    internal class DelegatingDataStoreForStreamTests : IDataStore
    {
        internal IDataStore WrappedStore;

        public bool StatusMonitoringEnabled => WrappedStore.StatusMonitoringEnabled;

        public void Dispose() => WrappedStore.Dispose();

        public ItemDescriptor? Get(DataKind kind, string key) => WrappedStore.Get(kind, key);

        public KeyedItems<ItemDescriptor> GetAll(DataKind kind) => WrappedStore.GetAll(kind);

        public void Init(FullDataSet<ItemDescriptor> allData) => WrappedStore.Init(allData);

        public bool Initialized() => WrappedStore.Initialized();

        public bool Upsert(DataKind kind, string key, ItemDescriptor item) => WrappedStore.Upsert(kind, key, item);
    }
}
