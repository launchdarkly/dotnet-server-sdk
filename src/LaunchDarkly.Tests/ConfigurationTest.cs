using System;
using LaunchDarkly.Client;
using NUnit.Framework;

namespace LaunchDarkly.Tests
{
    public class ConfigurationTest
    {
        [Test]
        public void CanOverrideConfiguration()
        {
            var config = Configuration.Default()
                                      .WithUri("https://app.AnyOtherEndpoint.com")
                                      .WithApiKey("AnyOtherApiKey")
                                      .WithEventQueueCapacity(99)
                                      .WithPollingInterval(TimeSpan.FromSeconds(1.5));
            
            Assert.AreEqual(new Uri("https://app.AnyOtherEndpoint.com"), config.BaseUri, "Configuration Base Uri");
            Assert.AreEqual("AnyOtherApiKey", config.ApiKey, "Configuration Api Key");
            Assert.AreEqual(99, config.EventQueueCapacity, "Event Queue Capacity");
            Assert.AreEqual(TimeSpan.FromSeconds(1.5), config.PollingInterval, "Polling Interval");
        }

        [Test]
        public void CannotOverrideTooSmallPollingInterval()
        {
            var config = Configuration.Default()
                          .WithPollingInterval(TimeSpan.FromMilliseconds(100));

            var expected = TimeSpan.FromSeconds(1);
            Assert.AreEqual(expected, config.PollingInterval, "Polling Interval");
        }
    }
}
