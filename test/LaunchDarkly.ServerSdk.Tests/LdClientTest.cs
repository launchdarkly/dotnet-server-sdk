using System;
using System.Collections.Generic;
using System.Linq;
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
            var config = Configuration.Builder("SDK_KEY")
                .IsStreamingEnabled(false)
                .BaseUri(new Uri("http://fake"))
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<DefaultEventProcessor>(client._eventProcessor);
            }
        }

        [Fact]
        public void StreamingClientHasStreamProcessor()
        {
            var config = Configuration.Builder("SDK_KEY")
                .IsStreamingEnabled(true)
                .BaseUri(new Uri("http://fake"))
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<StreamProcessor>(client._updateProcessor);
            }
        }

        [Fact]
        public void PollingClientHasPollingProcessor()
        {
            var config = Configuration.Builder("SDK_KEY")
                .IsStreamingEnabled(false)
                .BaseUri(new Uri("http://fake"))
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<PollingProcessor>(client._updateProcessor);
            }
        }

        [Fact]
        public void NoWaitForUpdateProcessorIfWaitMillisIsZero()
        {
            mockUpdateProcessor.Setup(up => up.Initialized()).Returns(true);
            var config = Configuration.Builder("SDK_KEY").StartWaitTime(TimeSpan.Zero)
                .UpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized());
            }
        }
        
        [Fact]
        public void UpdateProcessorCanTimeOut()
        {
            var config = Configuration.Builder("SDK_KEY").StartWaitTime(TimeSpan.FromMilliseconds(10))
                .UpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.False(client.Initialized());
            }
        }

        [Fact]
        public void ExceptionFromUpdateProcessorTaskDoesNotCauseExceptionInInit()
        {
            TaskCompletionSource<bool> errorTaskSource = new TaskCompletionSource<bool>();
            mockUpdateProcessor.Setup(up => up.Start()).Returns(errorTaskSource.Task);
            errorTaskSource.SetException(new Exception("bad"));
            var config = Configuration.Builder("SDK_KEY")
                .UpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.False(client.Initialized());
            }
        }

        [Fact]
        public void EvaluationReturnsDefaultValueIfNeitherClientNorFeatureStoreIsInited()
        {
            var featureStore = TestUtils.InMemoryFeatureStore();
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(1)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag); // but the store is still not inited

            var config = Configuration.Builder("SDK_KEY").StartWaitTime(TimeSpan.Zero)
                .FeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore))
                .UpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.Equal(0, client.IntVariation("key", User.WithKey("user"), 0));
            }
        }

        [Fact]
        public void EvaluationUsesFeatureStoreIfClientIsNotInitedButStoreIsInited()
        {
            var featureStore = TestUtils.InMemoryFeatureStore();
            featureStore.Init(new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>());
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(1)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            var config = Configuration.Builder("SDK_KEY").StartWaitTime(TimeSpan.Zero)
                .FeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore))
                .UpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.Equal(1, client.IntVariation("key", User.WithKey("user"), 0));
            }
        }

        [Fact]
        public void AllFlagsReturnsNullIfNeitherClientNorFeatureStoreIsInited()
        {
            var featureStore = TestUtils.InMemoryFeatureStore();
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(1)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag); // but the store is still not inited

            var config = Configuration.Builder("SDK_KEY").StartWaitTime(TimeSpan.Zero)
                .FeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore))
                .UpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
#pragma warning disable 0618
                Assert.Null(client.AllFlags(User.WithKey("user")));
#pragma warning restore 0618
            }
        }
        
        [Fact]
        public void AllFlagsUsesFeatureStoreIfClientIsNotInitedButStoreIsInited()
        {
            var featureStore = TestUtils.InMemoryFeatureStore();
            featureStore.Init(new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>());
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(1)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            var config = Configuration.Builder("SDK_KEY").StartWaitTime(TimeSpan.Zero)
                .FeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore))
                .UpdateProcessorFactory(TestUtils.SpecificUpdateProcessor(updateProcessor))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
#pragma warning disable 0618
                IDictionary<string, JToken> result = client.AllFlags(User.WithKey("user"));
#pragma warning restore 0618
                Assert.NotNull(result);
                Assert.Equal(new JValue(1), result["key"]);
            }
        }

        [Fact]
        public void DataSetIsPassedToFeatureStoreInCorrectOrder()
        {
            var mockStore = new Mock<IFeatureStore>();
            var store = mockStore.Object;
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> receivedData = null;

            mockStore.Setup(s => s.Init(It.IsAny<IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>>()))
                .Callback((IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> data) => {
                    receivedData = data;
                });

            mockUpdateProcessor.Setup(up => up.Start()).Returns(initTask);

            var config = Configuration.Builder("SDK_KEY")
                .FeatureStoreFactory(TestUtils.SpecificFeatureStore(store))
                .UpdateProcessorFactory(TestUtils.UpdateProcessorWithData(DependencyOrderingTestData))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.NotNull(receivedData);
                var entries = new List<KeyValuePair<IVersionedDataKind, IDictionary<string, IVersionedData>>>(
                    receivedData);
                Assert.Equal(DependencyOrderingTestData.Count, entries.Count);

                // Segments should always come first
                Assert.Equal(VersionedDataKind.Segments, entries[0].Key);
                Assert.Equal(DependencyOrderingTestData[VersionedDataKind.Segments].Count,
                    entries[0].Value.Count);

                // Features should be ordered so that a flag always appears after its prerequisites, if any
                Assert.Equal(VersionedDataKind.Features, entries[1].Key);
                Assert.Equal(DependencyOrderingTestData[VersionedDataKind.Features].Count,
                    entries[1].Value.Count);
                var flagsMap = entries[1].Value;
                var orderedItems = new List<IVersionedData>(flagsMap.Values);
                for (var itemIndex = 0; itemIndex < orderedItems.Count; itemIndex++)
                {
                    var item = orderedItems[itemIndex] as FeatureFlag;
                    foreach (var prereq in item.Prerequisites)
                    {
                        var depFlag = flagsMap[prereq.Key];
                        var depIndex = orderedItems.IndexOf(depFlag);
                        if (depIndex > itemIndex)
                        {
                            var allKeys = from i in orderedItems select i.Key;
                            Assert.True(false, String.Format("{0} depends on {1}, but {0} was listed first; keys in order are [{2}]",
                                item.Key, prereq.Key, String.Join(", ", allKeys)));
                        }
                    }
                }
            }
        }

        private static readonly IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>
            DependencyOrderingTestData = new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>()
        {
            {
                VersionedDataKind.Features,
                new Dictionary<string, IVersionedData>()
                {
                    { "a", new FeatureFlagBuilder("a")
                        .Prerequisites(new List<Prerequisite>() {
                            new Prerequisite("b", 0),
                            new Prerequisite("c", 0),
                            })
                        .Build() },
                    { "b", new FeatureFlagBuilder("b")
                        .Prerequisites(new List<Prerequisite>() {
                            new Prerequisite("c", 0),
                            new Prerequisite("e", 0),
                            })
                        .Build() },
                    { "c", new FeatureFlagBuilder("c").Build() },
                    { "d", new FeatureFlagBuilder("d").Build() },
                    { "e", new FeatureFlagBuilder("e").Build() },
                    { "f", new FeatureFlagBuilder("f").Build() }
                }
            },
            {
                VersionedDataKind.Segments,
                new Dictionary<string, IVersionedData>()
                {
                    { "o", new Segment("o", 1, null, null, null, null, false) }
                }
            }
        };
    }
}