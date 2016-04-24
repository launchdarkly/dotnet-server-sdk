using LaunchDarkly.Client;
using Moq;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

//TODO: use mock client
namespace LaunchDarkly.Tests
{
    class FeatureTest
    {
        private string feature_json = "{\"name\":\"New dashboard enable\",\"key\":\"new.dashboard.enable\",\"kind\":\"flag\",\"salt\":\"ZW5hYmxlLnRlYW0uc2lnbnVw\",\"on\":true,\"variations\":[{\"value\":true,\"weight\":0,\"targets\":[{\"attribute\":\"key\",\"op\":\"in\",\"values\":[\"user@test.com\"]}],\"userTarget\":{\"attribute\":\"key\",\"op\":\"in\",\"values\":[\"user@test.com\"]}},{\"value\":false,\"weight\":100,\"targets\":[{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}],\"userTarget\":{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}}],\"ttl\":0,\"commitDate\":\"2015-05-14T20:54:58.713Z\",\"creationDate\":\"2015-05-08T20:11:55.732Z\"}";
    
        [Ignore("ignored")]
        public void Toggle()
        {
            var config = Configuration.Default();
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond("application/json", feature_json);
            var eventStore = new Mock<IStoreEvents>();

            config.WithHttpClient(new HttpClient(mockHttp));
            var client = new LdClient(config, eventStore.Object);

            var user = User.WithKey("user@test.com");

            var result = client.Toggle("new.dashboard.enable", user);

            Assert.AreEqual(true, result);
        }

        [Test]
        public void IfAFeatureDoesNotExist_ToggleWillReturnDefault()
        {
            var config = Configuration.Default();

            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(HttpStatusCode.Unauthorized);

            var eventStore = new Mock<IStoreEvents>();
            config.WithHttpClient(new HttpClient(mockHttp));

            var client = new LdClient(config, eventStore.Object);

            var user = User.WithKey("anyUser");

            var result = client.Toggle("a.non.feature", user, false);

            Assert.AreEqual(false, result);
        }

        [Test]
        public void IfAFeatureDoesNotExist_TheDefaultCanBeOverridden()
        {
            var config = Configuration.Default();
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
            target.Values = new List<object>() { true};

            Assert.AreEqual(true, target.Matches(user));
        }
    }
}
