using System;
using LaunchDarkly.Client;
using NUnit.Framework;
using LaunchDarkly;

namespace LaunchDarkly.Tests
{
    public class ConfigureClient
    {
        [Test]
        public void CanOverrideConfiguration()
        {
            var config = Configuration.Default()
                                      .WithUri("https://app.AnyOtherEndpoint.com")
                                      .WithApiKey("AnyOtherApiKey")
                                      .WithEventQueueCapacity(99);
            
            Assert.AreEqual(new Uri("https://app.AnyOtherEndpoint.com"), config.BaseUri, "Configuration Base Uri");
            Assert.AreEqual("AnyOtherApiKey", config.ApiKey, "Configuration Api Key");
            Assert.AreEqual(99, config.EventQueueCapacity, "Event Queue Capacity");
        }

    }
}
