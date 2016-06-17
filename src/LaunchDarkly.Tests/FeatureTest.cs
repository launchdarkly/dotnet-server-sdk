using LaunchDarkly.Client;
using Moq;
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
            var target = new TargetRule();
            target.Attribute = "Org";
            target.Op = "in";
            target.Values = new List<object>() { 9, 55, 1362823, 292 };

            var user = User.WithKey("anyUser").AndCustomAttribute("Org", 1362823);
            Assert.IsTrue(target.Matches(user));

            user = User.WithKey("anyUser").AndCustomAttribute("Org", 55);
            Assert.IsTrue(target.Matches(user));

            user = User.WithKey("anyUser").AndCustomAttribute("Org", 292);
            Assert.IsTrue(target.Matches(user));
        }

        [Test]
        public void IfAUserHasCustomListAttributeAsIntegers_TargetRulesMatch()
        {

            var target = new TargetRule();
            target.Attribute = "Org";
            target.Op = "in";
            target.Values = new List<object>() { 55, 1362823 };

            var user = User.WithKey("anyUser").AndCustomAttribute("Org", new List<int>() { 1362823 });
            Assert.AreEqual(true, target.Matches(user));

            user = User.WithKey("anyUser").AndCustomAttribute("Org", new List<int>() { 55 });
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

        [Test]
        public void UserDoesNotHaveCustomAttributeSpecifiedInRule()
        {
            var target1 = new TargetRule();
            target1.Attribute = "Org";
            target1.Op = "in";
            target1.Values = new List<object>() { "100", 100.0, 100 };

            var target2 = new TargetRule();
            target2.Attribute = "";
            target2.Op = "in";
            target2.Values = new List<object>();

            var variation1 = new Variation();
            variation1.Value = true;
            variation1.Weight = 100;
            variation1.UserTarget = new TargetRule();
            variation1.UserTarget.Attribute = "key";
            variation1.UserTarget.Op = "in";
            variation1.UserTarget.Values = new List<object>();

            variation1.Targets = new List<TargetRule>();
            variation1.Targets.Add(target1);
            variation1.Targets.Add(target2);


            var variation2 = new Variation();
            variation2.Value = false;
            variation2.Weight = 0;
            variation2.UserTarget = new TargetRule();
            variation2.UserTarget.Attribute = "key";
            variation2.UserTarget.Op = "in";
            variation2.UserTarget.Values = new List<object>();

            var feature = new Feature();
            feature.Deleted = false;
            feature.On = true;
            feature.Variations = new List<Variation>();
            feature.Variations.Add(variation1);

            //Happy path- user has the targeted custom attribute
            var user = User.WithKey("anyUser").AndCustomAttribute("bizzle", 101);
            Assert.AreEqual(true, feature.Evaluate(user, false));

            //Test case: User does not have the targeted custom attribute
            user = User.WithKey("anyUser");
            Assert.AreEqual(true, feature.Evaluate(user, false));
        }
    }
}
