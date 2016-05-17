using LaunchDarkly.Client;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace LaunchDarkly.Tests
{
    class FeatureTest
    {
        private MockHttpMessageHandler mockHttp;
        private Configuration config;
        private LdClient client;

        [SetUp]
        public void Init()
        {
            config = Configuration.Default();
            mockHttp = new MockHttpMessageHandler();
            config.WithHttpClient(new HttpClient(mockHttp));
        }

        [TearDown]
        public void Dispose()
        {
            if (client != null)
                client.Dispose();
        }


        [Test]
        public void IfAFeatureDoesNotExist_ToggleWillReturnDefault()
        {
            mockHttp.When("*").Respond(HttpStatusCode.Unauthorized);

            var eventStore = new Mock<IStoreEvents>();

            client = new LdClient(config, eventStore.Object);

            var user = User.WithKey("anyUser");

            var result = client.Toggle("a.non.feature", user, false);

            Assert.AreEqual(false, result);
        }

        [Test]
        public void IfAFeatureDoesNotExist_TheDefaultCanBeOverridden()
        {
            var client = new LdClient(config);

            var user = User.WithKey("anyUser");

            var result = client.Toggle("a.non.feature", user, true);

            Assert.AreEqual(true, result);
        }

        [Test]
        public void IfAUserHasStringCustomAttributes_TargetRulesMatch()
        {
            var user = User.WithKey("anyUser").AndCustomAttribute("bizzle", "cripps");
            var target = new TargetRule();

            target.Attribute = "bizzle";
            target.Op = "in";
            target.Values = new List<object>() { "cripps" };

            Assert.AreEqual(true, target.Matches(user));
        }

        [Test]
        public void IfAUserHasCustomListAttributes_TargetRulesMatch()
        {
            var user = User.WithKey("anyUser").AndCustomAttribute("bizzle", new List<string>() { "cripps", "crupps" });
            var target = new TargetRule();

            target.Attribute = "bizzle";
            target.Op = "in";
            target.Values = new List<object>() { "cripps" };

            Assert.AreEqual(true, target.Matches(user));
        }

        [Test]
        public void IfAUserHasCustomAttributeAsFloat_TargetRulesMatch()
        {
            var user = User.WithKey("anyUser").AndCustomAttribute("numbers", 1362823F);
            var target = new TargetRule();

            target.Attribute = "numbers";
            target.Op = "in";
            target.Values = new List<object>() { 1362823F };

            Assert.AreEqual(true, target.Matches(user));
        }

        [Test]
        public void IfAUserHasCustomAttributeAsInteger_TargetRulesMatch()
        {
            var user = User.WithKey("anyUser").AndCustomAttribute("numbers", 1362823);
            var target = new TargetRule();

            target.Attribute = "numbers";
            target.Op = "in";
            target.Values = new List<object>() { 1362823.0 };

            Assert.AreEqual(true, target.Matches(user));
        }

        [Test]
        public void IfAUserHasCustomListAttributeAsIntegers_TargetRulesMatch()
        {
            var user = User.WithKey("anyUser").AndCustomAttribute("numbers", new List<int>() { 1362823 });
            var target = new TargetRule();

            target.Attribute = "numbers";
            target.Op = "in";
            target.Values = new List<object>() { 55, 1362823 };

            Assert.AreEqual(true, target.Matches(user));
        }

        [Test]
        public void IfAUserHasNonMatchingCustomListAttributes_TargetRulesDoNotMatch()
        {
            var user = User.WithKey("anyUser").AndCustomAttribute("bizzle", new List<string>() { "cruupps", "crupps" });
            var target = new TargetRule();

            target.Attribute = "bizzle";
            target.Op = "in";
            target.Values = new List<object>() { "cripps" };

            Assert.AreEqual(false, target.Matches(user));
        }

        [Test]
        public void IfAListHasCustomBoolAttributes_Target_rulesMatch()
        {
            var user = User.WithKey("anyUser").AndCustomAttribute("bizzle", true);
            var target = new TargetRule();

            target.Attribute = "bizzle";
            target.Op = "in";
            target.Values = new List<object>() { true };

            Assert.AreEqual(true, target.Matches(user));
        }
    }
}
