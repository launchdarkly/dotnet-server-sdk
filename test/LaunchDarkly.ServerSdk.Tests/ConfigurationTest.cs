using System;
using System.Net.Http;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using Xunit;

namespace LaunchDarkly.Sdk.Server
{
    public class ConfigurationTest
    {
        private readonly BuilderTestUtil<IConfigurationBuilder, Configuration> _tester =
            BuilderTestUtil.For(() => Configuration.Builder(sdkKey), b => b.Build())
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
        public void ConnectionTimeout()
        {
            var prop = _tester.Property(c => c.ConnectionTimeout, (b, v) => b.ConnectionTimeout(v));
            prop.AssertDefault(Configuration.DefaultConnectionTimeout);
            prop.AssertCanSet(TimeSpan.FromSeconds(7));
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
            prop.AssertCanSet(TestUtils.SpecificDataStore(new InMemoryDataStore()));
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
            prop.AssertCanSet(TestUtils.SpecificEventProcessor(new TestEventProcessor()));
        }

        [Fact]
        public void HttpMessageHandler()
        {
            var prop = _tester.Property(c => c.HttpMessageHandler, (b, v) => b.HttpMessageHandler(v));
            prop.AssertDefault(Configuration.DefaultMessageHandler);
            prop.AssertCanSet(new HttpClientHandler());
        }

        [Fact]
        public void Logging()
        {
            var prop = _tester.Property(c => c.LoggingConfigurationFactory, (b, v) => b.Logging(v));
            prop.AssertDefault(null);
            prop.AssertCanSet(Components.Logging(Logs.ToWriter(Console.Out)));
        }

        [Fact]
        public void Offline()
        {
            var prop = _tester.Property(c => c.Offline, (b, v) => b.Offline(v));
            prop.AssertDefault(false);
            prop.AssertCanSet(true);
        }

        [Fact]
        public void ReadTimeout()
        {
            var prop = _tester.Property(c => c.ReadTimeout, (b, v) => b.ReadTimeout(v));
            prop.AssertDefault(Configuration.DefaultReadTimeout);
            prop.AssertCanSet(TimeSpan.FromSeconds(7));
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
            prop.AssertDefault(Configuration.DefaultStartWaitTime);
            prop.AssertCanSet(TimeSpan.FromSeconds(7));
        }

        [Fact]
        public void WrapperName()
        {
            var prop = _tester.Property(c => c.WrapperName, (b, v) => b.WrapperName(v));
            prop.AssertDefault(null);
            prop.AssertCanSet("name");
        }

        [Fact]
        public void WrapperVersion()
        {
            var prop = _tester.Property(c => c.WrapperVersion, (b, v) => b.WrapperVersion(v));
            prop.AssertDefault(null);
            prop.AssertCanSet("1.0");
        }
    }
}