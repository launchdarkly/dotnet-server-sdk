using LaunchDarkly.Client;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace LaunchDarkly.Tests
{
    class FeatureRequestorTest
    {
        private string feature_json = @"{
              ""abc"": {
                ""key"": ""abc"",
                ""version"": 4,
                ""on"": true,
                ""prerequisites"": [],
                ""salt"": ""YWJj"",
                ""sel"": ""41e72130b42c414bac59fff3cf12a58e"",
                ""targets"": [
                  {
                    ""values"": [],
                    ""variation"": 0
                  },
                  {
                    ""values"": [],
                    ""variation"": 1
                  }
                ],
                ""rules"": [],
                ""fallthrough"": {
                  ""variation"": 1
                },
                ""offVariation"": null,
                ""variations"": [
                  true,
                  false
                ],
                ""deleted"": false
              },
              ""one-more-flag"": {
                ""key"": ""one-more-flag"",
                ""version"": 1,
                ""on"": false,
                ""prerequisites"": [],
                ""salt"": ""a2ee8e2dd521462ca7d60890678a6a5a"",
                ""sel"": ""8d586841cc6543a1aebd9da2cbd827e5"",
                ""targets"": [],
                ""rules"": [],
                ""fallthrough"": {
                  ""variation"": 3
                },
                ""offVariation"": null,
                ""variations"": [
                  ""a"",
                  ""b"",
                  ""c"",
                  ""d""
                ],
                ""deleted"": false
              }
            }";

        [Test]
        public async Task MakeAllRequestTest()
        {
            var config = Configuration.Default();
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://app.launchdarkly.com/sdk/latest-flags").Respond("application/json", feature_json);
            config.WithHttpClient(new HttpClient(mockHttp));
            config.WithSdkKey("SDK_KEY");
            FeatureRequestor featureRequestor = new FeatureRequestor(config);

            IDictionary<string, FeatureFlag> actual = await featureRequestor.MakeAllRequestAsync();
            Assert.AreEqual(2, actual.Count);
            Assert.IsTrue(actual.ContainsKey("abc"));
            Assert.IsTrue(actual.ContainsKey("one-more-flag"));
        }
    }
}
