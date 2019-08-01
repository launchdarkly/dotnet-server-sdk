using System;
using System.Net.Http;
using LaunchDarkly.Client;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class ConfigurationExtensionsTest
    {
        const string sdkKey = "any-key";

#pragma warning disable 618
        [Fact]
        public void CanOverrideConfiguration()
        {
            var config = Configuration.Default(sdkKey)
                .WithUri("https://app.AnyOtherEndpoint.com")
                .WithEventQueueCapacity(99)
                .WithPollingInterval(TimeSpan.FromMinutes(1));

            Assert.Equal(new Uri("https://app.AnyOtherEndpoint.com"), config.BaseUri);
            Assert.Equal(sdkKey, config.SdkKey);
            Assert.Equal(99, config.EventCapacity);
            Assert.Equal(TimeSpan.FromMinutes(1), config.PollingInterval);
        }

        [Fact]
        public void CanOverrideStreamConfiguration()
        {
            var config = Configuration.Default("AnyOtherSdkKey")
                .WithStreamUri("https://stream.AnyOtherEndpoint.com")
                .WithIsStreamingEnabled(false)
                .WithReadTimeout(TimeSpan.FromDays(1))
                .WithReconnectTime(TimeSpan.FromDays(1));

            Assert.Equal(new Uri("https://stream.AnyOtherEndpoint.com"), config.StreamUri);
            Assert.Equal(false, config.IsStreamingEnabled);
            Assert.Equal(TimeSpan.FromDays(1), config.ReadTimeout);
            Assert.Equal(TimeSpan.FromDays(1), config.ReconnectTime);
        }
        
        [Fact]
        public void CannotOverrideTooSmallPollingInterval()
        {
            var config = Configuration.Default("AnyOtherSdkKey").WithPollingInterval(TimeSpan.FromSeconds(29));

            Assert.Equal(TimeSpan.FromSeconds(30), config.PollingInterval);
        }

        [Fact]
        public void CanSetHttpClientHandler()
        {
            var handler = new HttpClientHandler();
            var config = Configuration.Default("AnyOtherSdkKey")
                .WithHttpClientHandler(handler);

            Assert.Equal(handler, config.HttpClientHandler);
        }
#pragma warning restore 618
    }
}