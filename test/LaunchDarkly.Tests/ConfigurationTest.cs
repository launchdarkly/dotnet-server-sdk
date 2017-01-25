using System;
using System.Net.Http;
using LaunchDarkly.Client;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class ConfigurationTest
    {
        [Fact]
        public void CanOverrideConfiguration()
        {
            var config = Configuration.Default("AnyOtherSdkKey")
                .WithUri("https://app.AnyOtherEndpoint.com")
                .WithEventQueueCapacity(99)
                .WithPollingInterval(TimeSpan.FromSeconds(1.5));

            Assert.Equal(new Uri("https://app.AnyOtherEndpoint.com"), config.BaseUri);
            Assert.Equal("AnyOtherSdkKey", config.SdkKey);
            Assert.Equal(99, config.EventQueueCapacity);
            Assert.Equal(TimeSpan.FromSeconds(1.5), config.PollingInterval);
        }

        [Fact]
        public void CannotOverrideTooSmallPollingInterval()
        {
            var config = Configuration.Default("AnyOtherSdkKey")
                .WithPollingInterval(TimeSpan.FromMilliseconds(100));

            var expected = TimeSpan.FromSeconds(1);
            Assert.Equal(expected, config.PollingInterval);
        }

        [Fact]
        public void CanSetHttpClientHandler()
        {
            var handler = new HttpClientHandler();
            var config = Configuration.Default("AnyOtherSdkKey")
                .WithHttpClientHandler(handler);

            Assert.Equal(handler, config.HttpClientHandler);
        }
    }
}