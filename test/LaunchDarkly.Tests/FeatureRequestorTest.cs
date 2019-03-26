using LaunchDarkly.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WireMock;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class FeatureRequestorTest : IDisposable
    {
        private const string AllDataJson = @"{""flags"":{""flag1"":{""key"":""flag1"",""version"":1}},""segments"":{""seg1"":{""key"":""seg1"",""version"":2}}}";

        private FluentMockServer _server;
        private IFeatureRequestor _requestor;

        public FeatureRequestorTest()
        {
            _server = FluentMockServer.Start();
            var config = Configuration.Default("key").WithUri(_server.Urls[0]).WithHttpClientTimeout(TimeSpan.FromDays(1));
            _requestor = new FeatureRequestor(config);
        }

        void IDisposable.Dispose()
        {
            _server.Stop();
        }

        [Fact]
        public async Task GetAllUsesCorrectUriAndParsesResponseAsync()
        {
            _server.Given(Request.Create().UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200).WithBody(AllDataJson));
            var result = await _requestor.GetAllDataAsync();

            var req = GetLastRequest();
            Assert.Equal("/sdk/latest-all", req.Path);

            Assert.Equal(1, result.Flags.Count);
            Assert.Equal(1, result.Flags["flag1"].Version);
            Assert.Equal(1, result.Segments.Count);
            Assert.Equal(2, result.Segments["seg1"].Version);
        }
        
        [Fact]
        public async Task GetAllStoresAndSendsEtag()
        {
            var etag = @"""abc123"""; // note that etag strings must be quoted
            _server.Given(Request.Create().UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Etag", etag).WithBody(AllDataJson));
            await _requestor.GetAllDataAsync();
            await _requestor.GetAllDataAsync();

            var reqs = new List<LogEntry>(_server.LogEntries);
            Assert.Equal(2, reqs.Count);
            Assert.False(reqs[0].RequestMessage.Headers.ContainsKey("If-None-Match"));
            Assert.Equal(new List<string> { etag }, reqs[1].RequestMessage.Headers["If-None-Match"]);
        }
        
        [Fact]
        public async Task GetAllReturnsNullIfNotModified()
        {
            var etag = @"""abc123"""; // note that etag strings must be quoted

            _server.Given(Request.Create().UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Etag", etag).WithBody(AllDataJson));
            var result1 = await _requestor.GetAllDataAsync();

            _server.Reset();
            _server.Given(Request.Create().UsingGet().WithHeader("If-None-Match", etag))
                .RespondWith(Response.Create().WithStatusCode(304));
            var result2 = await _requestor.GetAllDataAsync();

            Assert.NotNull(result1);
            Assert.Null(result2);
        }

        [Fact]
        public async Task GetFlagUsesCorrectUriAndParsesResponseAsync()
        {
            var json = @"{""key"":""flag1"",""version"":1}";
            _server.Given(Request.Create().UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200).WithBody(json));
            var flag = await _requestor.GetFlagAsync("flag1");

            var req = GetLastRequest();
            Assert.Equal("/sdk/latest-flags/flag1", req.Path);
            
            Assert.NotNull(flag);
            Assert.Equal("flag1", flag.Key);
            Assert.Equal(1, flag.Version);
        }

        [Fact]
        public async Task GetSegmentUsesCorrectUriAndParsesResponseAsync()
        {
            var json = @"{""key"":""seg1"",""version"":2}";
            _server.Given(Request.Create().UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200).WithBody(json));
            var segment = await _requestor.GetSegmentAsync("seg1");

            var req = GetLastRequest();
            Assert.Equal("/sdk/latest-segments/seg1", req.Path);

            Assert.NotNull(segment);
            Assert.Equal("seg1", segment.Key);
            Assert.Equal(2, segment.Version);
        }

        private RequestMessage GetLastRequest()
        {
            foreach (LogEntry le in _server.LogEntries)
            {
                return le.RequestMessage;
            }
            Assert.True(false, "Did not receive a request");
            return null;
        }
    }
}
