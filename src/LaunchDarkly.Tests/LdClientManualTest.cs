using LaunchDarkly.Client;
using NUnit.Framework;

namespace LaunchDarkly.Tests
{
    class LdClientManualTest
    {
        private static string SDK_KEY = "YOUR_SDK_KEY";
        private static string FEATURE_KEY = "YOUR_FEATURE_KEY";

        [Ignore("Manual")]
        //[Test]
        public void ManualTest()
        {
            Configuration config = Configuration.Default();
            config.WithSdkKey(SDK_KEY);
            FeatureRequestor featureRequestor = new FeatureRequestor(config);
            LdClient client = new LdClient(config);

            var user = User.WithKey("user@test.com");
            bool actual = client.BoolVariation(FEATURE_KEY, user, false);

            Assert.IsTrue(actual);
            client.Flush();
            client.Dispose();
        }
    }
}
