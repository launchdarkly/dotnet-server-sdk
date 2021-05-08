using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientEndToEndTest
    {
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
            using (var server = HttpServer.Start(Handlers.Status(202)))
            {
                var baseUri = server.Uri.ToString().TrimEnd('/') + baseUriExtraPath;
                var config = Configuration.Builder("key")
                    .DataSource(Components.ExternalUpdatesOnly)
                    .Events(Components.SendEvents().BaseUri(new Uri(baseUri)))
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
    }
}
