using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaunchDarkly.Client;
using LaunchDarkly.Common;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    // See also LDClientEvaluationTest, etc. This file contains mostly tests for the startup logic.
    public class LdClientTest
    {
        private Mock<IUpdateProcessor> mockUpdateProcessor;
        private IUpdateProcessor updateProcessor;
        private Task<bool> initTask;

        public LdClientTest()
        {
            mockUpdateProcessor = new Mock<IUpdateProcessor>();
            updateProcessor = mockUpdateProcessor.Object;
            initTask = Task.FromResult(true);
            mockUpdateProcessor.Setup(up => up.Start()).Returns(initTask);
        }
        
        [Fact]
        public void ClientHasDefaultEventProcessorByDefault()
        {
            var config = Configuration.Default("SDK_KEY")
                .WithIsStreamingEnabled(false)
                .WithUri(new Uri("http://fake"))
                .WithStartWaitTime(TimeSpan.Zero);
            using (var client = new LdClient(config))
            {
                Assert.IsType<DefaultEventProcessor>(client._eventProcessor);
            }
        }

        [Fact]
        public void StreamingClientHasStreamProcessor()
        {
            var config = Configuration.Default("SDK_KEY")
                .WithIsStreamingEnabled(true)
                .WithUri(new Uri("http://fake"))
                .WithStartWaitTime(TimeSpan.Zero);
            using (var client = new LdClient(config))
            {
                Assert.IsType<StreamProcessor>(client._updateProcessor);
            }
        }

        [Fact]
        public void PollingClientHasPollingProcessor()
        {
            var config = Configuration.Default("SDK_KEY")
                .WithIsStreamingEnabled(false)
                .WithUri(new Uri("http://fake"))
                .WithStartWaitTime(TimeSpan.Zero);
            using (var client = new LdClient(config))
            {
                Assert.IsType<PollingProcessor>(client._updateProcessor);
            }
        }

        [Fact]
        public void NoWaitForUpdateProcessorIfWaitMillisIsZero()
        {
            mockUpdateProcessor.Setup(up => up.Initialized()).Returns(true);
            var config = Configuration.Default("SDK_KEY").WithStartWaitTime(TimeSpan.Zero)
                .WithUpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .WithEventProcessorFactory(Components.NullEventProcessor);

            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized());
            }
        }
        
        [Fact]
        public void UpdateProcessorCanTimeOut()
        {
            var config = Configuration.Default("SDK_KEY").WithStartWaitTime(TimeSpan.FromMilliseconds(10))
                .WithUpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .WithEventProcessorFactory(Components.NullEventProcessor);

            using (var client = new LdClient(config))
            {
                Assert.False(client.Initialized());
            }
        }

        [Fact]
        public void EvaluationReturnsDefaultValueIfNeitherClientNorFeatureStoreIsInited()
        {
            var featureStore = new InMemoryFeatureStore();
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(1)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            var config = Configuration.Default("SDK_KEY").WithStartWaitTime(TimeSpan.Zero)
                .WithFeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore))
                .WithUpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .WithEventProcessorFactory(Components.NullEventProcessor);

            using (var client = new LdClient(config))
            {
                Assert.Equal(0, client.IntVariation("key", User.WithKey("user"), 0));
            }
        }

        [Fact]
        public void EvaluationUsesFeatureStoreIfClientIsNotInitedButStoreIsInited()
        {
            var featureStore = new InMemoryFeatureStore();
            featureStore.Init(new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>());
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(1)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            var config = Configuration.Default("SDK_KEY").WithStartWaitTime(TimeSpan.Zero)
                .WithFeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore))
                .WithUpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .WithEventProcessorFactory(Components.NullEventProcessor);

            using (var client = new LdClient(config))
            {
                Assert.Equal(1, client.IntVariation("key", User.WithKey("user"), 0));
            }
        }
    }
}