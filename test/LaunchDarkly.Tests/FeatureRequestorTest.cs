using System;
using LaunchDarkly.Client;
using System.Collections.Generic;
using System.Net.Http;
using Moq;
using Xunit;
using RichardSzalay.MockHttp;

namespace LaunchDarkly.Tests
{
    public class FeatureRequestorTest
    {
        private string feature_json =
            "{\"engine.enable\":{\"name\":\"New recommendations engine\",\"key\":\"engine.enable\",\"kind\":\"flag\",\"salt\":\"ZW5naW5lLmVuYWJsZQ==\",\"on\":true,\"variations\":[{\"value\":true,\"weight\":93,\"targets\":[{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}],\"userTarget\":{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}},{\"value\":false,\"weight\":7,\"targets\":[{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}],\"userTarget\":{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}}],\"commitDate\":\"2014-09-30T20:00:32.318Z\",\"creationDate\":\"2014-09-02T21:53:23.028Z\",\"version\":2},\"zentasks.gravatar\":{\"name\":\"Gravatar in header\",\"key\":\"zentasks.gravatar\",\"kind\":\"flag\",\"salt\":\"emVudGFza3MuZ3JhdmF0YXI=\",\"on\":true,\"variations\":[{\"value\":true,\"weight\":0,\"targets\":[{\"attribute\":\"key\",\"op\":\"in\",\"values\":[\"justin@persistiq.com\"]},{\"attribute\":\"country\",\"op\":\"in\",\"values\":[\"Canada\"]},{\"attribute\":\"customer_rank\",\"op\":\"in\",\"values\":[]}],\"userTarget\":{\"attribute\":\"key\",\"op\":\"in\",\"values\":[\"justin@persistiq.com\"]}},{\"value\":false,\"weight\":100,\"targets\":[{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]},{\"attribute\":\"groups\",\"op\":\"in\",\"values\":[]}],\"userTarget\":{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}}],\"commitDate\":\"2014-12-17T19:30:43.872Z\",\"creationDate\":\"2014-09-02T21:53:10.288Z\",\"version\":52}}";

//        [Fact]
        public async void MakeAllRequestTest()
        {
            //TODO: this doesn't actually mock the client.
            var mockConfig = new Mock<Configuration>();
            mockConfig.Setup(x => x.BaseUri).Returns(Configuration.DefaultUri);
            mockConfig.Setup(x => x.SdkKey).Returns("fakeSdkKey");
            var mockHttpHandler = new MockHttpMessageHandler();
            mockConfig.Setup(x => x.HttpClient()).Returns(new HttpClient(mockHttpHandler));
            mockHttpHandler.When("https://app.launchdarkly.com/sdk/latest-flags")
                .Respond("application/json", feature_json);

            FeatureRequestor featureRequestor = new FeatureRequestor(mockConfig.Object);

            IDictionary<string, FeatureFlag> actual = await featureRequestor.MakeAllRequestAsync();
            Assert.Equal(2, actual.Count);
            Assert.Contains("engine.enable", actual.Keys);
            Assert.Contains("zentasks.gravatar", actual.Keys);
        }
    }
}
