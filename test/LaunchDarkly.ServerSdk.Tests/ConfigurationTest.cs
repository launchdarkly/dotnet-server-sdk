using System;
using System.Collections.Generic;
using System.Net.Http;
using LaunchDarkly.Client;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class ConfigurationTest
    {
        const string sdkKey = "any-key";

        [Fact]
        public void DefaultSetsKey()
        {
            var config = Configuration.Default(sdkKey);
            Assert.Equal(sdkKey, config.SdkKey);
        }

        [Fact]
        public void BuilderSetsKey()
        {
            var config = Configuration.Builder(sdkKey).Build();
            Assert.Equal(sdkKey, config.SdkKey);
        }

        [Fact]
        public void CanSetProperties()
        {
            var uri = new Uri("http://fake");
            var time = TimeSpan.FromDays(3);
            TestSetter(b => b.BaseUri, c => c.BaseUri, uri);
            TestSetter(b => b.StreamUri, c => c.StreamUri, uri);
            TestSetter(b => b.EventsUri, c => c.EventsUri, uri);
            TestSetter(b => b.SdkKey, c => c.SdkKey, "other-key");
            TestSetter(b => b.IsStreamingEnabled, c => c.IsStreamingEnabled, false);
            TestSetter(b => b.EventCapacity, c => c.EventCapacity, 999);
            TestSetter(b => b.EventFlushInterval, c => c.EventFlushInterval, time);
            TestSetter(b => b.PollingInterval, c => c.PollingInterval, time);
            TestSetter(b => b.StartWaitTime, c => c.StartWaitTime, time);
            TestSetter(b => b.ReadTimeout, c => c.ReadTimeout, time);
            TestSetter(b => b.ReconnectTime, c => c.ReconnectTime, time);
            TestSetter(b => b.ConnectionTimeout, c => c.ConnectionTimeout, time);
            TestSetter(b => b.HttpMessageHandler, c => c.HttpMessageHandler, new HttpClientHandler());
            TestSetter(b => b.Offline, c => c.Offline, true);
            TestSetter(b => b.AllAttributesPrivate, c => c.AllAttributesPrivate, true);
            TestSetter(b => b.UserKeysCapacity, c => c.UserKeysCapacity, 999);
            TestSetter(b => b.UserKeysFlushInterval, c => c.UserKeysFlushInterval, time);
            TestSetter(b => b.InlineUsersInEvents, c => c.InlineUsersInEvents, true);
            TestSetter(b => b.UseLdd, c => c.UseLdd, true);
            TestSetter(b => b.DataStore, c => c.DataStoreFactory,
                TestUtils.SpecificDataStore(new InMemoryDataStore()));
            TestSetter(b => b.EventProcessorFactory, c => c.EventProcessorFactory,
                TestUtils.SpecificEventProcessor(new TestEventProcessor()));
            TestSetter(b => b.DataSource, c => c.DataSourceFactory,
                Components.NullDataSource);
        }

        private void TestSetter<T>(Func<IConfigurationBuilder, Func<T, IConfigurationBuilder>> setter,
            Func<Configuration, T> getter, T value)
        {
            var config = setter(Configuration.Builder(sdkKey))(value).Build();
            Assert.Equal(value, getter(config));
        }
        
        [Fact]
        public void CanSetPrivateAttributes()
        {
            var config = Configuration.Builder(sdkKey)
                .PrivateAttribute("a")
                .PrivateAttribute("b")
                .Build();
            var names = new List<string>(config.PrivateAttributeNames);
            Assert.Equal(2, config.PrivateAttributeNames.Count);
            Assert.Contains("a", config.PrivateAttributeNames);
            Assert.Contains("b", config.PrivateAttributeNames);
        }
        
        [Fact]
        public void CannotOverrideTooSmallPollingInterval()
        {
            var config = Configuration.Builder(sdkKey).PollingInterval(TimeSpan.FromSeconds(29)).Build();

            Assert.Equal(TimeSpan.FromSeconds(30), config.PollingInterval);
        }
        
        [Fact]
        public void DeprecatedPropertiesAreEquivalentToNewOnes()
        {
            var config = Configuration.Builder(sdkKey)
                .EventCapacity(99)
                .EventFlushInterval(TimeSpan.FromSeconds(90))
                .Build();

            Assert.Equal(99, config.EventCapacity);
            Assert.Equal(TimeSpan.FromSeconds(90), config.EventFlushInterval);
        }
    }
}