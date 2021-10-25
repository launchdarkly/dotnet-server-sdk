using System;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Json;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class FeatureRequestorTest : BaseTest
    {
        private static readonly FeatureFlag flag1 = new FeatureFlagBuilder("flag1").Version(1).On(true).Build();
        private static readonly Segment segment1 = new SegmentBuilder("seg1").Version(2).Build();

        private static readonly string AllDataJson = LdValue.BuildObject()
            .Add("flags", LdValue.BuildObject()
                .Add(flag1.Key, LdValue.Parse(LdJsonSerialization.SerializeObject(flag1)))
                .Build())
            .Add("segments", LdValue.BuildObject()
                .Add(segment1.Key, LdValue.Parse(LdJsonSerialization.SerializeObject(segment1)))
                .Build())
            .Build().ToJsonString();

        public FeatureRequestorTest(ITestOutputHelper testOutput) : base(testOutput) { }

        private IFeatureRequestor MakeRequestor(HttpServer server) => MakeRequestor(server.Uri);

        private IFeatureRequestor MakeRequestor(Uri baseUri)
        {
            var config = BasicConfig()
                .Http(Components.HttpConfiguration().ConnectTimeout(TimeSpan.FromDays(1)))
                .Build();
            return new FeatureRequestor(
                new LdClientContext(BasicContext.Basic, config),
                baseUri);
        }

        [Theory]
        [InlineData("", "/sdk/latest-all")]
        [InlineData("/basepath", "/basepath/sdk/latest-all")]
        [InlineData("/basepath/", "/basepath/sdk/latest-all")]
        public async Task GetAllUsesCorrectUriAndMethodAsync(
            string baseUriExtraPath,
            string expectedPath
            )
        {
            var resp = Handlers.BodyJson(AllDataJson);
            using (var server = HttpServer.Start(resp))
            {
                var config = Configuration.Builder("key")
                    .Http(Components.HttpConfiguration().ConnectTimeout(TimeSpan.FromDays(1)))
                    .Build();
                var baseUri = new Uri(server.Uri.ToString().TrimEnd('/') + baseUriExtraPath);

                using (var requestor = MakeRequestor(baseUri))
                {
                    await requestor.GetAllDataAsync();

                    var req = server.Recorder.RequireRequest();

                    Assert.Equal("GET", req.Method);
                    Assert.Equal(expectedPath, req.Path);
                }
            }
        }

        [Fact]
        public async Task GetAllParsesResponseAsync()
        {
            var resp = Handlers.BodyJson(AllDataJson);
            using (var server = HttpServer.Start(resp))
            {
                using (var requestor = MakeRequestor(server))
                {
                    var result = await requestor.GetAllDataAsync();

                    var req = server.Recorder.RequireRequest();
                    Assert.Equal("/sdk/latest-all", req.Path);

                    var expectedData = new DataSetBuilder().Flags(flag1).Segments(segment1).Build();
                    AssertHelpers.DataSetsEqual(expectedData, result.Value);
                }
            }
        }

        [Fact]
        public async Task GetAllStoresAndSendsEtag()
        {
            var etag = @"""abc123"""; // note that etag strings must be quoted
            var resp = Handlers.Header("Etag", etag).Then(Handlers.BodyJson(AllDataJson));
            using (var server = HttpServer.Start(resp))
            {
                using (var requestor = MakeRequestor(server))
                {
                    await requestor.GetAllDataAsync();
                    await requestor.GetAllDataAsync();

                    var req1 = server.Recorder.RequireRequest();
                    var req2 = server.Recorder.RequireRequest();
                    Assert.Null(req1.Headers.Get("If-None-Match"));
                    Assert.Equal(etag, req2.Headers.Get("If-None-Match"));
                    server.Recorder.RequireNoRequests(TimeSpan.FromMilliseconds(100));
                }
            }
        }

        [Fact]
        public async Task GetAllReturnsNullIfNotModified()
        {
            var etag = @"""abc123"""; // note that etag strings must be quoted
            using (var server = HttpServer.Start(Handlers.Switchable(out var switcher)))
            {
                using (var requestor = MakeRequestor(server))
                {
                    switcher.Target = Handlers.Header("Etag", etag).Then(Handlers.BodyJson(AllDataJson));
                    var result1 = await requestor.GetAllDataAsync();
                    Assert.NotNull(result1);

                    switcher.Target = Handlers.Status(304);
                    var result2 = await requestor.GetAllDataAsync();
                    Assert.Null(result2);
                }
            }
        }

        [Fact]
        public async Task GetAllDoesNotRetryFailedRequest()
        {
            using (var server = HttpServer.Start(Handlers.Status(503)))
            {
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

                    server.Recorder.RequireRequest();
                    server.Recorder.RequireNoRequests(TimeSpan.FromMilliseconds(100));
                }
            }
        }

        [Fact]
        public async Task ResponseWithoutEtagClearsPriorEtag()
        {
            var etag = @"""abc123""";
            using (var server = HttpServer.Start(Handlers.Switchable(out var switcher)))
            {
                using (var requestor = MakeRequestor(server))
                {
                    switcher.Target = Handlers.Header("Etag", etag).Then(Handlers.BodyJson(AllDataJson));
                    var result1 = await requestor.GetAllDataAsync();
                    var request1 = server.Recorder.RequireRequest();

                    switcher.Target = Handlers.BodyJson(AllDataJson); // respond with no etag this time
                    var result2 = await requestor.GetAllDataAsync();
                    var request2 = server.Recorder.RequireRequest();

                    var result3 = await requestor.GetAllDataAsync();
                    var request3 = server.Recorder.RequireRequest();

                    Assert.Null(request1.Headers.Get("If-None-Match"));
                    Assert.Equal(etag, request2.Headers.Get("If-None-Match"));
                    Assert.Null(request3.Headers.Get("If-None-Match"));
                }
            }
        }
    }
}
