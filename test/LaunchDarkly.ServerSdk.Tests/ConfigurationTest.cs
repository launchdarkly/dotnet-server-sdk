using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.TestHelpers;
using Xunit;

namespace LaunchDarkly.Sdk.Server
{
    public class ConfigurationTest
    {
        private readonly BuilderBehavior.BuildTester<ConfigurationBuilder, Configuration> _tester =
            BuilderBehavior.For(() => Configuration.Builder(sdkKey), b => b.Build())
                .WithCopyConstructor(c => Configuration.Builder(c));

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
        public void BigSegments()
        {
            var prop = _tester.Property(c => c.BigSegmentsConfigurationFactory, (b, v) => b.BigSegments(v));
            prop.AssertDefault(null);
            prop.AssertCanSet(Components.BigSegments(null));
        }

        [Fact]
        public void DataSource()
        {
            var prop = _tester.Property(c => c.DataSourceFactory, (b, v) => b.DataSource(v));
            prop.AssertDefault(null);
            prop.AssertCanSet(ComponentsImpl.NullDataSourceFactory.Instance);
        }

        [Fact]
        public void DataStore()
        {
            var prop = _tester.Property(c => c.DataStoreFactory, (b, v) => b.DataStore(v));
            prop.AssertDefault(null);
            prop.AssertCanSet(new InMemoryDataStore().AsSingletonFactory());
        }

        [Fact]
        public void DiagnosticOptOut()
        {
            var prop = _tester.Property(c => c.DiagnosticOptOut, (b, v) => b.DiagnosticOptOut(v));
            prop.AssertDefault(false);
            prop.AssertCanSet(true);
        }

        [Fact]
        public void Events()
        {
            var prop = _tester.Property(c => c.EventProcessorFactory, (b, v) => b.Events(v));
            prop.AssertDefault(null);
            prop.AssertCanSet(new MockEventProcessor().AsSingletonFactory());
        }

        [Fact]
        public void Logging()
        {
            var prop = _tester.Property(c => c.LoggingConfigurationFactory, (b, v) => b.Logging(v));
            prop.AssertDefault(null);
            prop.AssertCanSet(Components.Logging(Logs.ToWriter(Console.Out)));
        }

        [Fact]
        public void LoggingAdapterShortcut()
        {
            var adapter = Logs.ToWriter(Console.Out);
            var config = Configuration.Builder("").Logging(adapter).Build();
            var logConfig = config.LoggingConfigurationFactory.CreateLoggingConfiguration();
            Assert.Same(adapter, logConfig.LogAdapter);
        }

        [Fact]
        public void Offline()
        {
            var prop = _tester.Property(c => c.Offline, (b, v) => b.Offline(v));
            prop.AssertDefault(false);
            prop.AssertCanSet(true);
        }

        [Fact]
        public void SdkKey()
        {
            var prop = _tester.Property(c => c.SdkKey, (b, v) => b.SdkKey(v));
            prop.AssertCanSet("other-key");
        }

        [Fact]
        public void StartWaitTime()
        {
            var prop = _tester.Property(c => c.StartWaitTime, (b, v) => b.StartWaitTime(v));
            prop.AssertDefault(ConfigurationBuilder.DefaultStartWaitTime);
            prop.AssertCanSet(TimeSpan.FromSeconds(7));
        }
    }
}