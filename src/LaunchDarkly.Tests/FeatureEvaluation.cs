using LaunchDarkly.Client;
using Moq;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using System.Net;
using System.Net.Http;

namespace LaunchDarkly.Tests
{
    class FeatureEvaluation
    {
        private string feature_json = "{\"name\":\"New dashboard enable\",\"key\":\"new.dashboard.enable\",\"kind\":\"flag\",\"salt\":\"ZW5hYmxlLnRlYW0uc2lnbnVw\",\"on\":true,\"variations\":[{\"value\":true,\"weight\":0,\"targets\":[{\"attribute\":\"key\",\"op\":\"in\",\"values\":[\"user@test.com\"]}],\"userTarget\":{\"attribute\":\"key\",\"op\":\"in\",\"values\":[\"user@test.com\"]}},{\"value\":false,\"weight\":100,\"targets\":[{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}],\"userTarget\":{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}}],\"ttl\":0,\"commitDate\":\"2015-05-14T20:54:58.713Z\",\"creationDate\":\"2015-05-08T20:11:55.732Z\"}";
    
        [Test]
        public async void Toggle()
        {
            var config = Configuration.Default();
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond("application/json", feature_json);
            var eventStore = new Mock<IStoreEvents>();

            var client = new LdClient(config, new HttpClient(mockHttp), eventStore.Object);

            var user = User.WithKey("user@test.com");

            var result = await client.Toggle("new.dashboard.enable", user);

            Assert.AreEqual(true, result);
        }

        [Test]
        public async void IfAFeatureDoesNotExist_ToggleWillReturnDefault()
        {
            var config = Configuration.Default();

            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(HttpStatusCode.Unauthorized);

            var eventStore = new Mock<IStoreEvents>();

            var client = new LdClient(config, new HttpClient(mockHttp), eventStore.Object);

            var user = User.WithKey("anyUser");

            var result = await client.Toggle("a.non.feature", user, false);

            Assert.AreEqual(false, result);
        }

        [Test]
        public async void IfAFeatureDoesNotExist_TheDefaultCanBeOverridden()
        {
            var config = Configuration.Default();
            var client = new LdClient(config);

            var user = User.WithKey("anyUser");

            var result = await client.Toggle("a.non.feature", user, true);

            Assert.AreEqual(true, result);
        }


    }
}
