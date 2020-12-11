using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Server.Interfaces;
using WireMock;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class FeatureRequestorTest : BaseTest
    {
        private const string sdkKey = "SDK_KEY";
        private const string AllDataJson = @"{""flags"":{""flag1"":{""key"":""flag1"",""version"":1}},""segments"":{""seg1"":{""key"":""seg1"",""version"":2}}}";

        public FeatureRequestorTest(ITestOutputHelper testOutput) : base(testOutput) { }
        
        private IFeatureRequestor MakeRequestor(FluentMockServer server)
        {
            var config = Configuration.Builder(sdkKey)
                .Http(Components.HttpConfiguration().ConnectTimeout(TimeSpan.FromDays(1)))
                .Logging(Components.Logging(testLogging))
                .Build();
            return new FeatureRequestor(
                new LdClientContext(new BasicConfiguration(sdkKey, false, testLogger), config),
                new Uri(server.Urls[0]));
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
        public async Task GetFlagUsesCorrectUriAndParsesResponseAsync()
        {
            using (var server = await TestHttpUtils.StartServerAsync())
            {
                var json = @"{""key"":""flag1"",""version"":1}";
                server.Given(Request.Create().UsingGet())
                    .RespondWith(Response.Create().WithStatusCode(200).WithBody(json));

                using (var requestor = MakeRequestor(server))
                {
                    var flag = await requestor.GetFlagAsync("flag1");

                    var req = GetLastRequest(server);
                    Assert.Equal("/sdk/latest-flags/flag1", req.Path);

                    Assert.NotNull(flag);
                    Assert.Equal("flag1", flag.Key);
                    Assert.Equal(1, flag.Version);
                }
            }
        }

        [Fact]
        public async Task GetSegmentUsesCorrectUriAndParsesResponseAsync()
        {
            using (var server = await TestHttpUtils.StartServerAsync())
            {
                var json = @"{""key"":""seg1"",""version"":2}";
                server.Given(Request.Create().UsingGet())
                    .RespondWith(Response.Create().WithStatusCode(200).WithBody(json));

                using (var requestor = MakeRequestor(server))
                {
                    var segment = await requestor.GetSegmentAsync("seg1");

                    var req = GetLastRequest(server);
                    Assert.Equal("/sdk/latest-segments/seg1", req.Path);

                    Assert.NotNull(segment);
                    Assert.Equal("seg1", segment.Key);
                    Assert.Equal(2, segment.Version);
                }
            }
        }

        [Fact]
        public async Task ETagsDoNotConflict()
        {
            using (var server = await TestHttpUtils.StartServerAsync())
            {
                var etag1 = @"""abc123""";
                var etag2 = @"""def456""";
                var json1 = @"{""key"":""flag1"",""version"":1}";
                var json2 = @"{""key"":""flag2"",""version"":5}";

                server.Given(Request.Create().WithPath("/sdk/latest-flags/flag1").UsingGet().WithHeader("If-None-Match", etag1))
                    .AtPriority(1)
                    .RespondWith(Response.Create().WithStatusCode(304));
                server.Given(Request.Create().WithPath("/sdk/latest-flags/flag2").UsingGet().WithHeader("If-None-Match", etag2))
                    .AtPriority(1)
                    .RespondWith(Response.Create().WithStatusCode(304));
                server.Given(Request.Create().WithPath("/sdk/latest-flags/flag1").UsingGet())
                    .AtPriority(2)
                    .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Etag", etag1).WithBody(json1));
                server.Given(Request.Create().WithPath("/sdk/latest-flags/flag2").UsingGet())
                    .AtPriority(2)
                    .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Etag", etag2).WithBody(json2));

                using (var requestor = MakeRequestor(server))
                {
                    var fetch1 = await requestor.GetFlagAsync("flag1");
                    var fetch2 = await requestor.GetFlagAsync("flag1");
                    var fetch3 = await requestor.GetFlagAsync("flag2");
                    var fetch4 = await requestor.GetFlagAsync("flag2");
                    var fetch5 = await requestor.GetFlagAsync("flag1");

                    Assert.NotNull(fetch1);
                    Assert.Equal("flag1", fetch1.Key);
                    Assert.Null(fetch2);
                    Assert.NotNull(fetch3);
                    Assert.Equal("flag2", fetch3.Key);
                    Assert.Null(fetch4);
                    Assert.Null(fetch5);

                    var reqs = new List<LogEntry>(server.LogEntries);
                    Assert.Equal(5, reqs.Count);
                    Assert.False(reqs[0].RequestMessage.Headers.ContainsKey("If-None-Match"));
                    Assert.Equal(new List<string> { etag1 }, reqs[1].RequestMessage.Headers["If-None-Match"]);
                    Assert.False(reqs[2].RequestMessage.Headers.ContainsKey("If-None-Match"));
                    Assert.Equal(new List<string> { etag2 }, reqs[3].RequestMessage.Headers["If-None-Match"]);
                    Assert.Equal(new List<string> { etag1 }, reqs[4].RequestMessage.Headers["If-None-Match"]);
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
