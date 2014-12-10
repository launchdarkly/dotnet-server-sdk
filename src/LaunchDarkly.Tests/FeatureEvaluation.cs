using LaunchDarkly.Client;
using NUnit.Framework;

namespace LaunchDarkly.Tests
{
    class FeatureEvaluation
    {
        [Test]
        public void GetFlag()
        {
            var config = Configuration.Default();
            var client = new LdClient(config);

            var user = User.WithKey("user@test.com");

            var result = client.GetFlag("new.dashboard.enable", user);

            Assert.AreEqual(true, result);
        }

        [Ignore]
        public void GetSameFlag_GetsFromCache()
        {
            var config = Configuration.Default();
            var client = new LdClient(config);

            var user = User.WithKey("user@test.com");

            var result1 = client.GetFlag("new.dashboard.enable", user);
            var result2 = client.GetFlag("new.dashboard.enable", user);

        }


        [Test]
        public void IfAFeatureDoesNotExist_ItWillBeOffByDefault()
        {
            var config = Configuration.Default();
            var client = new LdClient(config);

            var user = User.WithKey("anyUser");

            var result = client.GetFlag("a.non.feature", user);

            Assert.AreEqual(false, result);
        }

        [Test]
        public void IfAFeatureDoesNotExist_TheDefaultCanBeOverridden()
        {
            var config = Configuration.Default();
            var client = new LdClient(config);

            var user = User.WithKey("anyUser");

            var result = client.GetFlag("a.non.feature", user, true);

            Assert.AreEqual(true, result);
        }


    }
}
