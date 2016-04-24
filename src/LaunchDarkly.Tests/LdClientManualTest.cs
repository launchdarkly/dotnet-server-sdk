using LaunchDarkly.Client;
using NUnit.Framework;
using System.Threading.Tasks;

namespace LaunchDarkly.Tests
{
    class LdClientManualTest
    {
        private static string API_KEY = "YOUR_API_KEY";
        private static string FEATURE_KEY = "YOUR_FEATURE_KEY";

        [Ignore("Manual")]
        //[Test]
        public async Task ManualTest()
        {
            Configuration config = Configuration.Default();
            config.WithApiKey(API_KEY);
            FeatureRequestor featureRequestor = new FeatureRequestor(config);
            LdClient client = new LdClient(config);

            var user = User.WithKey("user@test.com");
            bool actual = client.Toggle(FEATURE_KEY, user, false);
            System.Threading.Thread.Sleep(10000);

            Assert.IsTrue(actual);
            client.Flush();
            client.Dispose();
        }
    }
}
