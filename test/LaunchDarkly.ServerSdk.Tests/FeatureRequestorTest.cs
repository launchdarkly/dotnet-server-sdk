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
    public class FeatureRequestorTest
    {
        private const string AllDataJson = @"{""flags"":{""flag1"":{""key"":""flag1"",""version"":1}},""segments"":{""seg1"":{""key"":""seg1"",""version"":2}}}";
        
        private IFeatureRequestor MakeRequestor(FluentMockServer server)
        {
            var config = Configuration.Builder("key")
                .Http(Components.HttpConfiguration().ConnectTimeout(TimeSpan.FromDays(1)))
                .Build();
            return new FeatureRequestor(config, new Uri(server.Urls[0]));
        }

        [Fact]
        public async Task GetAllUsesCorrectUriAndParsesResponseAsync()
        {
            using (var server = await TestHttpUtils.StartServerAsync())
            {
                server.Given(Request.Create().UsingGet())
                    .RespondWith(Response.Create().WithStatusCode(200).WithBody(AllDataJson));

                using (var requestor = MakeRequestor(server))
                {
                    var result = await requestor.GetAllDataAsync();

                    var req = GetLastRequest(server);
                    Assert.Equal("/sdk/latest-all", req.Path);

                    Assert.Equal(1, result.Flags.Count);
                    Assert.Equal(1, result.Flags["flag1"].Version);
                    Assert.Equal(1, result.Segments.Count);
                    Assert.Equal(2, result.Segments["seg1"].Version);
                }
            }
        }

        [Fact]
        public async Task GetAllStoresAndSendsEtag()
        {
            using (var server = await TestHttpUtils.StartServerAsync())
            {
                var etag = @"""abc123"""; // note that etag strings must be quoted
                server.Given(Request.Create().UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Etag", etag).WithBody(AllDataJson));

                using (var requestor = MakeRequestor(server))
                {
                    await requestor.GetAllDataAsync();
                    await requestor.GetAllDataAsync();

                    var reqs = new List<LogEntry>(server.LogEntries);
                    Assert.Equal(2, reqs.Count);
                    Assert.False(reqs[0].RequestMessage.Headers.ContainsKey("If-None-Match"));
                    Assert.Equal(new List<string> { etag }, reqs[1].RequestMessage.Headers["If-None-Match"]);
                }
            }
        }

        [Fact]
        public async Task GetAllReturnsNullIfNotModified()
        {
            using (var server = await TestHttpUtils.StartServerAsync())
            {
                var etag = @"""abc123"""; // note that etag strings must be quoted

                server.Given(Request.Create().UsingGet())
                    .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Etag", etag).WithBody(AllDataJson));

                using (var requestor = MakeRequestor(server))
                {
                    var result1 = await requestor.GetAllDataAsync();

                    server.Reset();
                    server.Given(Request.Create().UsingGet().WithHeader("If-None-Match", etag))
                        .RespondWith(Response.Create().WithStatusCode(304));
                    var result2 = await requestor.GetAllDataAsync();

                    Assert.NotNull(result1);
                    Assert.Null(result2);
                }
            }
        }

        [Fact]
        public async Task GetAllDoesNotRetryFailedRequest()
        {
            using (var server = await TestHttpUtils.StartServerAsync())
            {
                server.Given(Request.Create().UsingGet())
                    .RespondWith(Response.Create().WithStatusCode(503));

                using (var requestor = MakeRequestor(server))
                {
                    try
                    {
                        await requestor.GetAllDataAsync();
                    }
                    catch (UnsuccessfulResponseException e)
                    {
                        Assert.Equal(503, e.StatusCode);
                    }

                    var reqs = new List<LogEntry>(server.LogEntries);
                    Assert.Equal(1, reqs.Count);
                }
            }
        }

        [Fact]
        public async Task ResponseWithoutEtagClearsPriorEtag()
        {
            using (var server = await TestHttpUtils.StartServerAsync())
            {
                var etag = @"""abc123""";

                server.Given(Request.Create().UsingGet())
                    .AtPriority(2)
                    .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Etag", etag).WithBody(AllDataJson));
                server.Given(Request.Create().UsingGet().WithHeader("If-None-Match", etag))
                    .AtPriority(1)
                    .RespondWith(Response.Create().WithStatusCode(200).WithBody(AllDataJson)); // respond with no etag

                using (var requestor = MakeRequestor(server))
                {
                    var fetch1 = await requestor.GetAllDataAsync();
                    var fetch2 = await requestor.GetAllDataAsync();

                    server.Given(Request.Create().UsingGet())
                        .AtPriority(1)
                        .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Etag", etag).WithBody(AllDataJson));

                    var fetch3 = await requestor.GetAllDataAsync();

                    var reqs = new List<LogEntry>(server.LogEntries);
                    Assert.Equal(3, reqs.Count);
                    Assert.False(reqs[0].RequestMessage.Headers.ContainsKey("If-None-Match"));
                    Assert.Equal(new List<string> { etag }, reqs[1].RequestMessage.Headers["If-None-Match"]);
                    Assert.False(reqs[2].RequestMessage.Headers.ContainsKey("If-None-Match"));
                }
            }
        }

        private RequestMessage GetLastRequest(FluentMockServer server)
        {
            foreach (LogEntry le in server.LogEntries)
            {
                return le.RequestMessage;
            }
            Assert.True(false, "Did not receive a request");
            return null;
        }
    }
}
