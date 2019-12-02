using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataSources;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Model;
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
            // The underlying functionality here is also covered in DataStoreSorterTest, but we want to verify that the
            // client object is actually *using* DataStoreSorter.

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
                .DataSource(TestUtils.DataSourceWithData(DataStoreSorterTest.DependencyOrderingTestData))
                .EventProcessorFactory(Components.NullEventProcessor)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.NotNull(receivedData);
                DataStoreSorterTest.VerifyDataSetOrder(receivedData, DataStoreSorterTest.DependencyOrderingTestData,
                    DataStoreSorterTest.ExpectedOrderingForSortedDataSet);
            }
        }
    }
}