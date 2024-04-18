using System;
using System.Net.Http;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientServiceEndpointsTests : BaseTest
    {
        // These tests verify that the SDK is using the expected base URIs in various configurations.
        // Since we need to be able to intercept requests that would normally go to the production service
        // endpoints, and we don't care about simulating realistic responses, we'll just use a simple
        // HttpMessageHandler stub.

        private static readonly Uri CustomUri = new Uri("http://custom");

        private readonly RequestRecorder _requestRecorder;
        private readonly HttpMessageHandler _stubHandler;

        public LdClientServiceEndpointsTests(ITestOutputHelper testOutput) : base(testOutput)
        {
            _stubHandler = Handlers.Record(out _requestRecorder)
                .Then(Handlers.Status(401))
                .AsMessageHandler();
        }

        [Fact]
        public void DefaultStreamingDataSourceBaseUri()
        {
            using (var client = new LdClient(
                BasicConfig()
                    .DataSource(Components.StreamingDataSource())
                    .Http(Components.HttpConfiguration().MessageHandler(_stubHandler))
                    .Build()))
            {
                var req = _requestRecorder.RequireRequest();
                Assert.Equal(StandardEndpoints.DefaultStreamingBaseUri, BaseUriOf(req.Uri));
            }
        }

        [Fact]
        public void DefaultPollingDataSourceBaseUri()
        {
            using (var client = new LdClient(
                BasicConfig()
                    .DataSource(Components.PollingDataSource())
                    .Http(Components.HttpConfiguration().MessageHandler(_stubHandler))
                    .Build()))
            {
                var req = _requestRecorder.RequireRequest();
                Assert.Equal(StandardEndpoints.DefaultPollingBaseUri, BaseUriOf(req.Uri));
            }
        }

        [Fact]
        public void DefaultEventsBaseUri()
        {
            using (var client = new LdClient(
                BasicConfig()
                    .Events(Components.SendEvents())
                    .Http(Components.HttpConfiguration().MessageHandler(_stubHandler))
                    .Build()))
            {
                var req = _requestRecorder.RequireRequest();
                Assert.Equal(StandardEndpoints.DefaultEventsBaseUri, BaseUriOf(req.Uri));
            }
        }

        [Fact]
        public void CustomStreamingDataSourceBaseUri()
        {
            using (var client = new LdClient(
                BasicConfig()
                    .DataSource(Components.StreamingDataSource())
                    .Http(Components.HttpConfiguration().MessageHandler(_stubHandler))
                    .ServiceEndpoints(Components.ServiceEndpoints().Streaming(CustomUri))
                    .Build()))
            {
                var req = _requestRecorder.RequireRequest();
                Assert.Equal(CustomUri, BaseUriOf(req.Uri));

                Assert.False(LogCapture.HasMessageWithRegex(LogLevel.Error,
                    "You have set custom ServiceEndpoints without specifying"));
            }
        }

        [Fact]
        public void CustomPollingDataSourceBaseUri()
        {
            using (var client = new LdClient(
                BasicConfig()
                    .DataSource(Components.PollingDataSource())
                    .Http(Components.HttpConfiguration().MessageHandler(_stubHandler))
                    .ServiceEndpoints(Components.ServiceEndpoints().Polling(CustomUri))
                    .Build()))
            {
                var req = _requestRecorder.RequireRequest();
                Assert.Equal(CustomUri, BaseUriOf(req.Uri));

                Assert.False(LogCapture.HasMessageWithRegex(LogLevel.Error,
                    "You have set custom ServiceEndpoints without specifying"));
            }
        }

        [Fact]
        public void CustomEventsBaseUri()
        {
            using (var client = new LdClient(
                BasicConfig()
                    .Events(Components.SendEvents())
                    .Http(Components.HttpConfiguration().MessageHandler(_stubHandler))
                    .ServiceEndpoints(Components.ServiceEndpoints().Events(CustomUri))
                    .Build()))
            {
                var req = _requestRecorder.RequireRequest();
                Assert.Equal(CustomUri, BaseUriOf(req.Uri));

                AssertLogMessageRegex(false, LogLevel.Error,
                    "You have set custom ServiceEndpoints without specifying");
            }
        }

        [Fact]
        public void ErrorIsLoggedIfANecessaryUriIsNotSetWhenOtherCustomUrisAreSet()
        {
            var logCapture1 = Logs.Capture();
            using (var client = new LdClient(
                BasicConfig()
                    .DataSource(Components.StreamingDataSource())
                    .Http(Components.HttpConfiguration().MessageHandler(_stubHandler))
                    .Logging(logCapture1)
                    .ServiceEndpoints(Components.ServiceEndpoints().Polling(CustomUri))
                    .Build()))
            {
                Assert.True(logCapture1.HasMessageWithRegex(LogLevel.Error,
                    "You have set custom ServiceEndpoints without specifying the Streaming base URI"));
            }

            var logCapture2 = Logs.Capture();
            using (var client = new LdClient(
                BasicConfig()
                    .DataSource(Components.PollingDataSource())
                    .Http(Components.HttpConfiguration().MessageHandler(_stubHandler))
                    .Logging(logCapture2)
                    .ServiceEndpoints(Components.ServiceEndpoints().Events(CustomUri))
                    .Build()))
            {
                Assert.True(logCapture2.HasMessageWithRegex(LogLevel.Error,
                    "You have set custom ServiceEndpoints without specifying the Polling base URI"));
            }

            var logCapture3 = Logs.Capture();
            using (var client = new LdClient(
                BasicConfig()
                    .Events(Components.SendEvents())
                    .Http(Components.HttpConfiguration().MessageHandler(_stubHandler))
                    .Logging(logCapture3)
                    .ServiceEndpoints(Components.ServiceEndpoints().Streaming(CustomUri))
                    .Build()))
            {
                Assert.True(logCapture3.HasMessageWithRegex(LogLevel.Error,
                    "You have set custom ServiceEndpoints without specifying the Events base URI"));
            }
        }

        private static Uri BaseUriOf(Uri uri) =>
            new Uri(uri.GetComponents(UriComponents.Scheme | UriComponents.HostAndPort | UriComponents.KeepDelimiter, UriFormat.Unescaped));
    }
}
