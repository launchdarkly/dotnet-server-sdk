using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using WireMock.Server;
using WireMock.Settings;

namespace LaunchDarkly.Sdk.Server
{
    internal static class TestHttpUtils
    {
        internal static async Task<FluentMockServer> StartServerAsync()
        {
            var settings = new FluentMockServerSettings() { StartTimeout = 2000 };
            var server = FluentMockServer.Start(settings);
            // We've sometimes seen some instability in FluentMockServer that causes the server to not really be
            // ready when Start() returns, so we'll poll it to make sure.
            int nAttempts = 0;
            using (var client = new HttpClient())
            {
                while (true)
                {
                    try
                    {
                        await client.GetAsync(server.Urls[0]);
                        break;
                    }
                    catch (HttpRequestException)
                    {
                        if (nAttempts++ > 10)
                        {
                            throw new Exception("Test HTTP server did not become available within a reasonable time");
                        }
                        await Task.Delay(50);
                    }
                }
            }
            server.ResetLogEntries();
            return server;
        }

        internal class StubMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _body;
            private readonly string _contentType;

            internal StubMessageHandler(HttpStatusCode status) : this(status, null, null) { }

            internal StubMessageHandler(HttpStatusCode status, string body, string contentType)
            {
                _status = status;
                _body = body;
                _contentType = contentType;
            }
    
            internal static StubMessageHandler EmptyPollingResponse() =>
                new StubMessageHandler(HttpStatusCode.OK, "{}", "application/json");

            internal static StubMessageHandler EmptyStreamingResponse() =>
                new StubMessageHandler(HttpStatusCode.OK, "", "text/event-stream");

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var resp = new HttpResponseMessage(_status);
                if (_body != null)
                {
                    resp.Content = new StringContent(_body, System.Text.Encoding.UTF8, _contentType);
                }
                return Task.FromResult(new HttpResponseMessage(_status));
            }
        }
    }
}
