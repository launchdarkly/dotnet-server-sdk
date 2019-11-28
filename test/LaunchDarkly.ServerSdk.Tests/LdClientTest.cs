using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Model;
using Moq;
using Xunit;

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
        public void DiagnosticStorePassedToFactoriesWhenSupported()
        {
            var epfwd = new Mock<IEventProcessorFactoryWithDiagnostics>();
            var dsfwd = new Mock<IDataSourceFactoryWithDiagnostics>();
            var config = Configuration.Builder("SDK_KEY")
                .IsStreamingEnabled(false)
                .BaseUri(new Uri("http://fake"))
                .StartWaitTime(TimeSpan.Zero)
                .EventProcessorFactory(epfwd.Object)
                .DataSource(dsfwd.Object)
                .Build();

            IDiagnosticStore eventProcessorDiagnosticStore = null;
            IDiagnosticStore updateProcessorDiagnosticStore = null;

            epfwd.Setup(epf => epf.CreateEventProcessor(config, It.IsAny<IDiagnosticStore>()))
                .Callback<Configuration, IDiagnosticStore>((c, ds) => eventProcessorDiagnosticStore = ds)
                .Returns(Components.NullEventProcessor.CreateEventProcessor(config));
            dsfwd.Setup(dsf => dsf.CreateDataSource(config, It.IsAny<IDataStore>(), It.IsAny<IDiagnosticStore>()))
                .Callback<Configuration, IDataStore, IDiagnosticStore>((c, fs, ds) => updateProcessorDiagnosticStore = ds)
                .Returns(dataSource);

            using (var client = new LdClient(config))
            {
                epfwd.Verify(epf => epf.CreateEventProcessor(config, It.IsNotNull<IDiagnosticStore>()), Times.Once());
                epfwd.VerifyNoOtherCalls();
                dsfwd.Verify(dsf => dsf.CreateDataSource(config, It.IsNotNull<IDataStore>(), It.IsNotNull<IDiagnosticStore>()), Times.Once());
                dsfwd.VerifyNoOtherCalls();
                Assert.True(eventProcessorDiagnosticStore == updateProcessorDiagnosticStore);
            }
        }

        [Fact]
        public void DiagnosticStoreNotPassedToFactoriesWhenOptedOut()
        {
            var epfwd = new Mock<IEventProcessorFactoryWithDiagnostics>();
            var dsfwd = new Mock<IDataSourceFactoryWithDiagnostics>();
            var config = Configuration.Builder("SDK_KEY")
                .IsStreamingEnabled(false)
                .BaseUri(new Uri("http://fake"))
                .StartWaitTime(TimeSpan.Zero)
                .EventProcessorFactory(epfwd.Object)
                .DataSource(dsfwd.Object)
                .DiagnosticOptOut(true)
                .Build();

            epfwd.Setup(epf => epf.CreateEventProcessor(config, It.IsAny<IDiagnosticStore>()))
                .Returns(Components.NullEventProcessor.CreateEventProcessor(config));
            dsfwd.Setup(upf => upf.CreateDataSource(config, It.IsAny<IDataStore>(), It.IsAny<IDiagnosticStore>()))
                .Returns(dataSource);

            using (var client = new LdClient(config))
            {
                epfwd.Verify(epf => epf.CreateEventProcessor(config, null), Times.Once());
                epfwd.VerifyNoOtherCalls();
                dsfwd.Verify(dsf => dsf.CreateDataSource(config, It.IsNotNull<IDataStore>(), null), Times.Once());
                dsfwd.VerifyNoOtherCalls();
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
            dataStore.Upsert(VersionedDataKind.Features, flag); // but the store is still not inited

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
            dataStore.Init(new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>());
            var flag = new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(1)).Build();
            dataStore.Upsert(VersionedDataKind.Features, flag);

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
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> receivedData = null;

            mockStore.Setup(s => s.Init(It.IsAny<IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>>()))
                .Callback((IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> data) => {
                    receivedData = data;
                });

            mockDataSource.Setup(up => up.Start()).Returns(initTask);

            var config = Configuration.Builder("SDK_KEY")
                .DataStore(TestUtils.SpecificDataStore(store))
                .DataSource(TestUtils.DataSourceWithData(DependencyOrderingTestData))
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