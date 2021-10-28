using System;
using System.Linq;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.MockResponses;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientEndToEndTest : BaseTest
    {
        private static readonly FeatureFlag AlwaysTrueFlag = new FeatureFlagBuilder("always-true-flag")
            .OffWithValue(LdValue.Of(true)).Build();

        private static Handler ValidPollingResponse =>
            PollingResponse(MakeExpectedData().Build());

        private static Handler ValidStreamingResponse =>
            StreamWithInitialData(MakeExpectedData().Build());

        public LdClientEndToEndTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void ClientStartsInStreamingMode()
        {
            using (var streamServer = HttpServer.Start(ValidStreamingResponse))
            {
                var config = BasicConfig()
                    .DataSource(Components.StreamingDataSource())
                    .ServiceEndpoints(Components.ServiceEndpoints().Streaming(streamServer.Uri))
                    .StartWaitTime(TimeSpan.FromSeconds(5))
                    .Build();

                using (var client = new LdClient(config))
                {
                    VerifyClientStartedAndHasExpectedData(client, streamServer);

                    Assert.Empty(LogCapture.GetMessages().Where(m => m.Level == Logging.LogLevel.Warn));
                    Assert.Empty(LogCapture.GetMessages().Where(m => m.Level == Logging.LogLevel.Error));
                }
            }
        }

        [Fact]
        public void ClientFailsToStartInStreamingModeWith401Error()
        {
            using (var streamServer = HttpServer.Start(Error401Response))
            {
                var config = BasicConfig()
                    .DataSource(Components.StreamingDataSource())
                    .ServiceEndpoints(Components.ServiceEndpoints().Streaming(streamServer.Uri))
                    .StartWaitTime(TimeSpan.FromSeconds(5))
                    .Build();

                using (var client = new LdClient(config))
                {
                    Assert.False(client.Initialized);
                    Assert.Equal(DataSourceState.Off, client.DataSourceStatusProvider.Status.State);

                    var value = client.BoolVariation(AlwaysTrueFlag.Key, BasicUser, false);
                    Assert.False(value);

                    var request = streamServer.Recorder.RequireRequest();
                    Assert.Equal(BasicSdkKey, request.Headers.Get("Authorization"));

                    Assert.NotEmpty(LogCapture.GetMessages().Where(
                        m => m.Level == Logging.LogLevel.Error && m.Text.Contains("error 401") &&
                            m.Text.Contains("giving up permanently")));
                }
            }
        }

        [Fact]
        public void ClientRetriesConnectionInStreamingModeWithNonFatalError()
        {
            var failThenSucceedHandler = Handlers.Sequential(Error503Response, ValidStreamingResponse);

            using (var streamServer = HttpServer.Start(failThenSucceedHandler))
            {
                var config = BasicConfig()
                    .DataSource(Components.StreamingDataSource())
                    .ServiceEndpoints(Components.ServiceEndpoints().Streaming(streamServer.Uri))
                    .StartWaitTime(TimeSpan.FromSeconds(5))
                    .Build();

                using (var client = new LdClient(config))
                {
                    Assert.True(client.Initialized);
                    Assert.Equal(DataSourceState.Valid, client.DataSourceStatusProvider.Status.State);

                    var value = client.BoolVariation(AlwaysTrueFlag.Key, BasicUser, false);
                    Assert.True(value);

                    var request1 = streamServer.Recorder.RequireRequest();
                    var request2 = streamServer.Recorder.RequireRequest();
                    Assert.Equal(BasicSdkKey, request1.Headers.Get("Authorization"));
                    Assert.Equal(BasicSdkKey, request2.Headers.Get("Authorization"));

                    Assert.NotEmpty(LogCapture.GetMessages().Where(
                        m => m.Level == Logging.LogLevel.Warn && m.Text.Contains("error 503") &&
                            m.Text.Contains("will retry")));
                    Assert.Empty(LogCapture.GetMessages().Where(m => m.Level == Logging.LogLevel.Error));
                }
            }
        }

        [Fact]
        public void ClientStartsInPollingMode()
        {
            using (var pollServer = HttpServer.Start(ValidPollingResponse))
            {
                var config = BasicConfig()
                    .DataSource(Components.PollingDataSource())
                    .ServiceEndpoints(Components.ServiceEndpoints().Polling(pollServer.Uri))
                    .StartWaitTime(TimeSpan.FromSeconds(5))
                    .Build();

                using (var client = new LdClient(config))
                {
                    VerifyClientStartedAndHasExpectedData(client, pollServer);

                    Assert.NotEmpty(LogCapture.GetMessages().Where(
                        m => m.Level == Logging.LogLevel.Warn &&
                            m.Text.Contains("You should only disable the streaming API if instructed to do so")));
                    Assert.Empty(LogCapture.GetMessages().Where(m => m.Level == Logging.LogLevel.Error));
                }
            }
        }

        [Fact]
        public void ClientFailsToStartInPollingModeWith401Error()
        {
            using (var pollServer = HttpServer.Start(Error401Response))
            {
                var config = BasicConfig()
                    .DataSource(Components.PollingDataSource())
                    .ServiceEndpoints(Components.ServiceEndpoints().Polling(pollServer.Uri))
                    .StartWaitTime(TimeSpan.FromSeconds(5))
                    .Build();

                using (var client = new LdClient(config))
                {
                    Assert.False(client.Initialized);
                    Assert.Equal(DataSourceState.Off, client.DataSourceStatusProvider.Status.State);

                    var value = client.BoolVariation(AlwaysTrueFlag.Key, BasicUser, false);
                    Assert.False(value);

                    var request = pollServer.Recorder.RequireRequest();
                    Assert.Equal(BasicSdkKey, request.Headers.Get("Authorization"));

                    Assert.NotEmpty(LogCapture.GetMessages().Where(
                        m => m.Level == Logging.LogLevel.Error && m.Text.Contains("error 401") &&
                            m.Text.Contains("giving up permanently")));
                }
            }
        }

        [Fact]
        public void ClientRetriesConnectionInPollingModeWithNonFatalError()
        {
            var failThenSucceedHandler = Handlers.Sequential(Error503Response, ValidPollingResponse);

            using (var pollServer = HttpServer.Start(failThenSucceedHandler))
            {
                var config = BasicConfig()
                    .DataSource(Components.PollingDataSource()
                        .PollIntervalNoMinimum(TimeSpan.FromMilliseconds(50)))
                    .ServiceEndpoints(Components.ServiceEndpoints().Polling(pollServer.Uri))
                    .StartWaitTime(TimeSpan.FromSeconds(5))
                    .Build();

                using (var client = new LdClient(config))
                {
                    Assert.True(client.Initialized);
                    Assert.Equal(DataSourceState.Valid, client.DataSourceStatusProvider.Status.State);

                    var value = client.BoolVariation(AlwaysTrueFlag.Key, BasicUser, false);
                    Assert.True(value);

                    var request1 = pollServer.Recorder.RequireRequest();
                    var request2 = pollServer.Recorder.RequireRequest();
                    Assert.Equal(BasicSdkKey, request1.Headers.Get("Authorization"));
                    Assert.Equal(BasicSdkKey, request2.Headers.Get("Authorization"));

                    Assert.NotEmpty(LogCapture.GetMessages().Where(
                        m => m.Level == Logging.LogLevel.Warn && m.Text.Contains("error 503") &&
                            m.Text.Contains("will retry")));
                    Assert.Empty(LogCapture.GetMessages().Where(m => m.Level == Logging.LogLevel.Error));
                }
            }
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("/", "")]
        [InlineData("/basepath", "/basepath")]
        [InlineData("/basepath/", "/basepath")]
        public void EventsAreSentToCorrectEndpoints(
            string baseUriExtraPath,
            string expectedBasePath
            )
        {
            using (var server = HttpServer.Start(EventsAcceptedResponse))
            {
                var baseUri = server.Uri.ToString().TrimEnd('/') + baseUriExtraPath;
                var config = BasicConfig()
                    .Events(Components.SendEvents())
                    .ServiceEndpoints(Components.ServiceEndpoints().Events(baseUri))
                    .StartWaitTime(TimeSpan.FromSeconds(5))
                    .Build();

                using (var client = new LdClient(config))
                {
                    client.Identify(User.WithKey("userkey"));
                    client.Flush();

                    var request1 = server.Recorder.RequireRequest();
                    var request2 = server.Recorder.RequireRequest();

                    if (request1.Path.EndsWith("diagnostic"))
                    {
                        var temp = request1;
                        request1 = request2;
                        request2 = temp;
                    }

                    Assert.Equal("POST", request1.Method.ToUpper());
                    Assert.Equal(expectedBasePath + "/bulk", request1.Path);

                    Assert.Equal("POST", request2.Method.ToUpper());
                    Assert.Equal(expectedBasePath + "/diagnostic", request2.Path);
                }
            }
        }

        [Fact]
        public void HttpConfigurationIsAppliedToStreaming()
        {
            TestHttpUtils.TestWithSpecialHttpConfigurations(
                ValidStreamingResponse,
                (targetUri, httpConfig, server) =>
                {
                    var config = BasicConfig()
                        .DataSource(Components.StreamingDataSource())
                        .Http(httpConfig)
                        .ServiceEndpoints(Components.ServiceEndpoints().Streaming(targetUri))
                        .StartWaitTime(TimeSpan.FromSeconds(5))
                        .Build();
                    using (var client = new LdClient(config))
                    {
                        VerifyClientStartedAndHasExpectedData(client, server);
                    }
                },
                TestLogger
                );
        }

        [Fact]
        public void HttpConfigurationIsAppliedToPolling()
        {
            TestHttpUtils.TestWithSpecialHttpConfigurations(
                ValidPollingResponse,
                (targetUri, httpConfig, server) =>
                {
                    var config = BasicConfig()
                        .DataSource(Components.PollingDataSource())
                        .Http(httpConfig)
                        .ServiceEndpoints(Components.ServiceEndpoints().Polling(targetUri))
                        .StartWaitTime(TimeSpan.FromSeconds(5))
                        .Build();
                    using (var client = new LdClient(config))
                    {
                        VerifyClientStartedAndHasExpectedData(client, server);
                    }
                },
                TestLogger
                );
        }

        [Fact]
        public void HttpConfigurationIsAppliedToEvents()
        {
            TestHttpUtils.TestWithSpecialHttpConfigurations(
                EventsAcceptedResponse,
                (targetUri, httpConfig, server) =>
                {
                    var config = BasicConfig()
                        .DiagnosticOptOut(true)
                        .Events(Components.SendEvents())
                        .Http(httpConfig)
                        .ServiceEndpoints(Components.ServiceEndpoints().Events(targetUri))
                        .StartWaitTime(TimeSpan.FromSeconds(5))
                        .Build();
                    using (var client = new LdClient(config))
                    {
                        client.Identify(User.WithKey("userkey"));
                        client.Flush();
                        server.Recorder.RequireRequest();
                    }
                },
                TestLogger
                );
        }

        private static DataSetBuilder MakeExpectedData() => new DataSetBuilder().Flags(AlwaysTrueFlag);

        private static void VerifyClientStartedAndHasExpectedData(LdClient client, HttpServer server)
        {
            Assert.Equal(DataSourceState.Valid, client.DataSourceStatusProvider.Status.State);
            Assert.True(client.Initialized);

            var value = client.BoolVariation(AlwaysTrueFlag.Key, BasicUser, false);
            Assert.True(value);

            var request = server.Recorder.RequireRequest();
            Assert.Equal(BasicSdkKey, request.Headers.Get("Authorization"));
        }
    }
}
