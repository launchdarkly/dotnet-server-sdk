using LaunchDarkly.Client;
using NUnit.Framework;
using System.Threading.Tasks;

namespace LaunchDarkly.Tests
{
    class LdClientManualTest
    {
        private static string API_KEY = "sdk-707fa2a8-f3be-4f14-a122-946ab580a648";
        private static string FEATURE_KEY = "YOUR_FEATURE_KEY";

        [Ignore("Manual")]
        //[Test]
        public void ManualTest()
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
