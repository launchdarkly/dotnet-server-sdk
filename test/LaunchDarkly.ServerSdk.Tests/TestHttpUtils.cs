using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;

namespace LaunchDarkly.Sdk.Server
{
    internal static class TestHttpUtils
    {
        public static readonly Uri FakeUri = new Uri("http://not-real");

        internal class MessageHandlerThatAddsPathSuffix : HttpClientHandler
        {
            private readonly string _suffix;

            internal MessageHandlerThatAddsPathSuffix(string suffix) { _suffix = suffix; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                request.RequestUri = new Uri(request.RequestUri.ToString() + _suffix);
                return base.SendAsync(request, cancellationToken);
            }
        }

        // Used for TestWithSpecialHttpConfigurations
        public delegate void HttpConfigurationTestAction(Uri targetUri, IHttpConfigurationFactory httpConfig, HttpServer server);

        /// <summary>
        /// A test suite for all SDK components that support our standard HTTP configuration options.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Although all of our supported HTTP behaviors are implemented in shared code, there is no
        /// guarantee that all of our components are using that code, or using it correctly. So we
        /// should run this test suite on each component that can be affected by HttpConfigurationBuilder
        /// properties.
        /// </para>
        /// <para>
        /// For each HTTP configuration variant that is expected work (e.g., using a proxy server), it
        /// sets up a server that will produce whatever expected response was specified in
        /// <paramref name="responseHandler"/>. Then it runs <paramref name="testActionShouldSucceed"/>,
        /// which should create its component with the given configuration and base URI and verify that
        /// the component behaves correctly.
        /// </para>
        /// </remarks>
        /// <param name="responseHandler">specifies how the target server should response</param>
        /// <param name="testActionShouldSucceed">verifies the result of a test</param>
        /// <param name="log">the current TestLogger</param>
        public static void TestWithSpecialHttpConfigurations(
            Handler responseHandler,
            HttpConfigurationTestAction testActionShouldSucceed,
            Logger log
            )
        {
            log.Info("*** TestHttpClientCanUseCustomMessageHandler");
            TestHttpClientCanUseCustomMessageHandler(responseHandler, testActionShouldSucceed);

            log.Info("*** TestHttpClientCanUseProxy");
            TestHttpClientCanUseProxy(responseHandler, testActionShouldSucceed);
        }

        static void TestHttpClientCanUseCustomMessageHandler(Handler responseHandler,
            HttpConfigurationTestAction testActionShouldSucceed)
        {
            // To verify that a custom HttpMessageHandler will really be used if provided, we
            // create one that behaves normally except that it modifies the request path.
            // Then we verify that the server received a request with a modified path.

            var recordAndDelegate = Handlers.Record(out var recorder).Then(responseHandler);
            using (var server = HttpServer.Start(recordAndDelegate))
            {
                var suffix = "/modified-by-test";
                var messageHandler = new MessageHandlerThatAddsPathSuffix(suffix);
                var httpConfig = Components.HttpConfiguration().MessageHandler(messageHandler);

                testActionShouldSucceed(server.Uri, httpConfig, server);

                var request = recorder.RequireRequest();
                Assert.EndsWith(suffix, request.Path);
                recorder.RequireNoRequests(TimeSpan.FromMilliseconds(100));
            }
        }

        static void TestHttpClientCanUseProxy(Handler responseHandler,
            HttpConfigurationTestAction testActionShouldSucceed)
        {
            // To verify that a web proxy will really be used if provided, we set up a proxy
            // configuration pointing to our test server. It's not really a proxy server,
            // but if it receives a request that was intended for some other URI (instead of
            // the SDK trying to access that other URI directly), then that's a success.

            using (var server = HttpServer.Start(responseHandler))
            {
                var proxy = new WebProxy(server.Uri);
                var httpConfig = Components.HttpConfiguration().Proxy(proxy);
                var fakeBaseUri = new Uri("http://not-a-real-host");

                testActionShouldSucceed(fakeBaseUri, httpConfig, server);
            }
        }

        public struct ServerErrorCondition
        {
            public const int FakeIOException = -1; // constant to be used in the constructor

            public int StatusCode { get; set; }
            public Exception IOException { get; set; }

            public static ServerErrorCondition FromStatus(int status)
            {
                return new ServerErrorCondition
                {
                    StatusCode = status,
                    IOException = status == FakeIOException ? new IOException("deliberate error") : null
                };
            }

            public bool Recoverable => IOException != null || HttpErrors.IsRecoverable(StatusCode);

            public Handler Handler =>
                IOException is null ? Handlers.Status(StatusCode) : Handlers.Error(IOException);

            public void VerifyDataSourceStatusError(DataSourceStatus status)
            {
                Assert.Equal(Recoverable ? DataSourceState.Interrupted : DataSourceState.Off, status.State);
                Assert.NotNull(status.LastError);
                Assert.Equal(
                    IOException is null
                        ? DataSourceStatus.ErrorKind.ErrorResponse
                        : DataSourceStatus.ErrorKind.NetworkError,
                    status.LastError.Value.Kind);
                Assert.Equal(
                    IOException is null ? StatusCode : 0,
                    status.LastError.Value.StatusCode
                    );
                if (IOException != null)
                {
                    Assert.Contains(IOException.Message, status.LastError.Value.Message);
                }
            }

            public void VerifyLogMessage(LogCapture logCapture)
            {
                var level = Recoverable ? LogLevel.Warn : LogLevel.Error;
                var message = (IOException is null)
                    ? "HTTP error " + StatusCode + ".*" + (Recoverable ? "will retry" : "giving up")
                    : IOException.Message;
                AssertHelpers.LogMessageRegex(logCapture, true, level, message);
            }
        }

        /// <summary>
        /// Sets up the HttpTest framework to simulate a server error of some kind. If
        /// it is an HTTP error response, we'll use an embedded HttpServer. If it is an
        /// I/O error, we have to use a custom message handler instead.
        /// </summary>
        /// <param name="errorCondition"></param>
        /// <param name="successResponseAfterError">if not null, the second request will
        /// receive this response instead of the error</param>
        /// <param name="action"></param>
        public static void WithServerErrorCondition(ServerErrorCondition errorCondition,
            Handler successResponseAfterError,
            Action<Uri, IHttpConfigurationFactory, RequestRecorder> action)
        {
            var responseHandler = successResponseAfterError is null ? errorCondition.Handler :
                Handlers.Sequential(errorCondition.Handler, successResponseAfterError);
            if (errorCondition.IOException is null)
            {
                using (var server = HttpServer.Start(responseHandler))
                {
                    action(server.Uri, Components.HttpConfiguration(), server.Recorder);
                }
            }
            else
            {
                var handler = Handlers.Record(out var recorder).Then(responseHandler);
                action(
                    FakeUri,
                    Components.HttpConfiguration().MessageHandler(handler.AsMessageHandler()),
                    recorder
                    );
            }
        }
    }
}
