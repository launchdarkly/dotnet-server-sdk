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
            TestSetter(b => b.AllAttributesPrivate, c => c.AllAttributesPrivate, true);
            TestSetter(b => b.BaseUri, c => c.BaseUri, uri);
            TestSetter(b => b.StreamUri, c => c.StreamUri, uri);
            TestSetter(b => b.DiagnosticOptOut, c => c.DiagnosticOptOut, true);
            TestSetter(b => b.DiagnosticRecordingInterval, c => c.DiagnosticRecordingInterval, time);
            TestSetter(b => b.EventCapacity, c => c.EventCapacity, 999);
            TestSetter(b => b.EventFlushInterval, c => c.EventFlushInterval, time);
            TestSetter(b => b.EventProcessorFactory, c => c.EventProcessorFactory,
                TestUtils.SpecificEventProcessor(new TestEventProcessor()));
            TestSetter(b => b.EventsUri, c => c.EventsUri, uri);
            TestSetter(b => b.FeatureStoreFactory, c => c.FeatureStoreFactory,
                TestUtils.SpecificFeatureStore(TestUtils.InMemoryFeatureStore()));
            TestSetter(b => b.HttpClientHandler, c => c.HttpClientHandler, new HttpClientHandler());
            TestSetter(b => b.HttpClientTimeout, c => c.HttpClientTimeout, time);
            TestSetter(b => b.InlineUsersInEvents, c => c.InlineUsersInEvents, true);
            TestSetter(b => b.IsStreamingEnabled, c => c.IsStreamingEnabled, false);
            TestSetter(b => b.Offline, c => c.Offline, true);
            TestSetter(b => b.PollingInterval, c => c.PollingInterval, time);
            TestSetter(b => b.ReadTimeout, c => c.ReadTimeout, time);
            TestSetter(b => b.ReconnectTime, c => c.ReconnectTime, time);
            TestSetter(b => b.SdkKey, c => c.SdkKey, "other-key");
            TestSetter(b => b.StartWaitTime, c => c.StartWaitTime, time);
            TestSetter(b => b.UpdateProcessorFactory, c => c.UpdateProcessorFactory,
                Components.NullUpdateProcessor);
            TestSetter(b => b.UseLdd, c => c.UseLdd, true);
            TestSetter(b => b.UserKeysCapacity, c => c.UserKeysCapacity, 999);
            TestSetter(b => b.UserKeysFlushInterval, c => c.UserKeysFlushInterval, time);
            TestSetter(b => b.WrapperName, c => c.WrapperName, "name");
            TestSetter(b => b.WrapperVersion, c => c.WrapperVersion, "version");
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
        public void CannotOverrideTooSmallDiagnosticRecordingInterval()
        {
            var config = Configuration.Builder(sdkKey).DiagnosticRecordingInterval(TimeSpan.FromSeconds(59)).Build();

            Assert.Equal(TimeSpan.FromMinutes(1), config.DiagnosticRecordingInterval);
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
#pragma warning disable 618
            Assert.Equal(config.EventCapacity, config.EventQueueCapacity);
            Assert.Equal(config.EventFlushInterval, config.EventQueueFrequency);
#pragma warning restore 618
        }
    }
}