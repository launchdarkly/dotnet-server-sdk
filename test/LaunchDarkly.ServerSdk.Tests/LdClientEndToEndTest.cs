using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using LaunchDarkly.Client;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class LdClientEndToEndTest
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("/", "")]
        [InlineData("/basepath", "/basepath")]
        [InlineData("/basepath/", "/basepath")]
        public async Task EventsAreSentToCorrectEndpointsAsync(
            string baseUriExtraPath,
            string expectedBasePath
            )
        {
            using (var server = await TestHttpUtils.StartServerAsync())
            {
                var requests = new BlockingCollection<RequestMessage>();
                server.Given(Request.Create()).RespondWith(Response.Create().WithCallback(req =>
                {
                    requests.Add(req);
                    return new ResponseMessage() { StatusCode = 202 };

                }));

                var baseUri = server.Urls[0].TrimEnd('/') + baseUriExtraPath;
                var config = Configuration.Builder("key")
                    .DataSource(Components.ExternalUpdatesOnly)
                    .Events(Components.SendEvents().BaseUri(new Uri(baseUri)))
                    .Build();

                using (var client = new LdClient(config))
                {
                    client.Identify(User.WithKey("userkey"));
                    client.Flush();

                    Assert.True(requests.TryTake(out var request1, TimeSpan.FromSeconds(5)));
                    Assert.True(requests.TryTake(out var request2, TimeSpan.FromSeconds(5)));

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
    }
}
