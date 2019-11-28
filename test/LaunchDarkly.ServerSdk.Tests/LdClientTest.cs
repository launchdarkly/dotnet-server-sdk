using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Model;
using Moq;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server
{
    // See also LDClientEvaluationTest, etc. This file contains mostly tests for the startup logic.
    public class LdClientTest
    {
        private readonly Mock<IDataSource> mockDataSource;
        private readonly IDataSource dataSource;
        private readonly Task<bool> initTask;

        public LdClientTest()
        {
            mockDataSource = new Mock<IDataSource>();
            dataSource = mockDataSource.Object;
            initTask = Task.FromResult(true);
            mockDataSource.Setup(up => up.Start()).Returns(initTask);
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
                Assert.IsType<StreamProcessor>(client._dataSource);
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
                Assert.IsType<PollingProcessor>(client._dataSource);
            }
        }

        [Fact]
        public void NoWaitForDataSourceIfWaitMillisIsZero()
        {
            mockDataSource.Setup(up => up.Initialized()).Returns(true);
            var config = Configuration.Builder("SDK_KEY").StartWaitTime(TimeSpan.Zero)
                .DataSource(TestUtils.SpecificDataSource(dataSource))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized());
            }
        }
        
        [Fact]
        public void DataSourceCanTimeOut()
        {
            var config = Configuration.Builder("SDK_KEY").StartWaitTime(TimeSpan.FromMilliseconds(10))
                .DataSource(TestUtils.SpecificDataSource(dataSource))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.False(client.Initialized());
            }
        }

        [Fact]
        public void ExceptionFromDataSourceTaskDoesNotCauseExceptionInInit()
        {
            TaskCompletionSource<bool> errorTaskSource = new TaskCompletionSource<bool>();
            mockDataSource.Setup(up => up.Start()).Returns(errorTaskSource.Task);
            errorTaskSource.SetException(new Exception("bad"));
            var config = Configuration.Builder("SDK_KEY")
                .DataSource(TestUtils.SpecificDataSource(dataSource))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.False(client.Initialized());
            }
        }

        [Fact]
        public void EvaluationReturnsDefaultValueIfNeitherClientNorDataStoreIsInited()
        {
            var dataStore = new InMemoryDataStore();
            var flag = new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(1)).Build();
            TestUtils.UpsertFlag(dataStore, flag);
            // note, the store is still not inited

            var config = Configuration.Builder("SDK_KEY").StartWaitTime(TimeSpan.Zero)
                .DataStore(TestUtils.SpecificDataStore(dataStore))
                .DataSource(TestUtils.SpecificDataSource(dataSource))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.Equal(0, client.IntVariation("key", User.WithKey("user"), 0));
            }
        }

        [Fact]
        public void EvaluationUsesDataStoreIfClientIsNotInitedButStoreIsInited()
        {
            var dataStore = new InMemoryDataStore();
            dataStore.Init(FullDataSet<ItemDescriptor>.Empty());
            var flag = new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(1)).Build();
            TestUtils.UpsertFlag(dataStore, flag);

            var config = Configuration.Builder("SDK_KEY").StartWaitTime(TimeSpan.Zero)
                .DataStore(TestUtils.SpecificDataStore(dataStore))
                .DataSource(TestUtils.SpecificDataSource(dataSource))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.Equal(1, client.IntVariation("key", User.WithKey("user"), 0));
            }
        }
        
        [Fact]
        public void DataSetIsPassedToDataStoreInCorrectOrder()
        {
            var mockStore = new Mock<IDataStore>();
            var store = mockStore.Object;
            FullDataSet<ItemDescriptor> receivedData;

            mockStore.Setup(s => s.Init(It.IsAny<FullDataSet<ItemDescriptor>>()))
                .Callback((FullDataSet<ItemDescriptor> data) => {
                    receivedData = data;
                });

            mockDataSource.Setup(up => up.Start()).Returns(initTask);

            var config = Configuration.Builder("SDK_KEY")
                .DataStore(TestUtils.SpecificDataStore(store))
                .DataSource(TestUtils.DataSourceWithData(new FullDataSet<ItemDescriptor>(DependencyOrderingTestData)))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.NotNull(receivedData);
                var entries = receivedData.Data.ToList();
                Assert.Equal(DependencyOrderingTestData.Count, entries.Count);

                // Segments should always come first
                Assert.Equal(DataKinds.Segments, entries[0].Key);
                Assert.Equal(DependencyOrderingTestData[DataKinds.Segments].Count(),
                    entries[0].Value.Count());

                // Features should be ordered so that a flag always appears after its prerequisites, if any
                Assert.Equal(DataKinds.Features, entries[1].Key);
                Assert.Equal(DependencyOrderingTestData[DataKinds.Features].Count(),
                    entries[1].Value.Count());
                var flags = entries[1].Value;
                var orderedItems = new List<FeatureFlag>(flags.Select(kv => kv.Value.Item as FeatureFlag));
                for (var itemIndex = 0; itemIndex < orderedItems.Count; itemIndex++)
                {
                    var item = orderedItems[itemIndex];
                    foreach (var prereq in item.Prerequisites)
                    {
                        var depFlag = flags.First(kv => kv.Key == prereq.Key).Value.Item as FeatureFlag;
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

        private static readonly Dictionary<DataKind, IEnumerable<KeyValuePair<string, ItemDescriptor>>> DependencyOrderingTestData =
            new Dictionary<DataKind, IEnumerable<KeyValuePair<string, ItemDescriptor>>>()
        {
            {
                DataKinds.Features,
                new Dictionary<string, ItemDescriptor>()
                {
                    { "a", new ItemDescriptor(1, new FeatureFlagBuilder("a")
                        .Prerequisites(new List<Prerequisite>() {
                            new Prerequisite("b", 0),
                            new Prerequisite("c", 0),
                            })
                        .Build()) },
                    { "b", new ItemDescriptor(1, new FeatureFlagBuilder("b")
                        .Prerequisites(new List<Prerequisite>() {
                            new Prerequisite("c", 0),
                            new Prerequisite("e", 0),
                            })
                        .Build()) },
                    { "c", new ItemDescriptor(1, new FeatureFlagBuilder("c").Build()) },
                    { "d", new ItemDescriptor(1, new FeatureFlagBuilder("d").Build()) },
                    { "e", new ItemDescriptor(1, new FeatureFlagBuilder("e").Build()) },
                    { "f", new ItemDescriptor(1, new FeatureFlagBuilder("f").Build()) }
                }
            },
            {
                DataKinds.Segments,
                new Dictionary<string, ItemDescriptor>()
                {
                    { "o", new ItemDescriptor(1, new Segment("o", 1, null, null, null, null, false)) }
                }
            }
        };
    }
}