using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.Sdk.Server
{
    internal static class TestHttpUtils
    {
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
