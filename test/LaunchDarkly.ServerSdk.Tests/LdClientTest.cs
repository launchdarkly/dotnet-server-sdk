using System;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
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
        private const string sdkKey = "SDK_KEY";

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
        public void ClientStartupMessage()
        {
            var logCapture = Logs.Capture();
            var config = Configuration.Builder(sdkKey)
                .Logging(Components.Logging(logCapture))
                .Events(Components.NoEvents)
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.True(logCapture.HasMessageWithText(LogLevel.Info,
                    "Starting LaunchDarkly Client " + ServerSideClientEnvironment.Instance.Version),
                    logCapture.ToString());
            }
        }

        [Fact]
        public void ClientHasDefaultEventProcessorByDefault()
        {
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .StartWaitTime(TimeSpan.Zero)
                .DiagnosticOptOut(true)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<DefaultEventProcessorWrapper>(client._eventProcessor);
            }
        }

        [Fact]
        public void StreamingClientHasStreamProcessor()
        {
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.StreamingDataSource().BaseUri(new Uri("http://fake")))
                .Events(Components.NoEvents)
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<StreamProcessor>(client._dataSource);
            }
        }

        [Fact]
        public void StreamingClientStartupMessage()
        {
            var logCapture = Logs.Capture();
            var config = Configuration.Builder(sdkKey)
                .Logging(Components.Logging(logCapture))
                .DataSource(Components.StreamingDataSource().BaseUri(new Uri("http://fake")))
                .Events(Components.NoEvents)
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.False(logCapture.HasMessageWithText(LogLevel.Warn,
                    "You should only disable the streaming API if instructed to do so by LaunchDarkly support"),
                    logCapture.ToString());
            }
        }

        [Fact]
        public void PollingClientHasPollingProcessor()
        {
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.PollingDataSource().BaseUri(new Uri("http://fake")))
                .Events(Components.NoEvents)
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<PollingProcessor>(client._dataSource);
            }
        }

        [Fact]
        public void PollingClientStartupMessage()
        {
            var logCapture = Logs.Capture();
            var config = Configuration.Builder(sdkKey)
                .Logging(Components.Logging(logCapture))
                .Events(Components.NoEvents)
                .DataSource(Components.PollingDataSource().BaseUri(new Uri("http://fake")))
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.True(logCapture.HasMessageWithText(LogLevel.Warn,
                    "You should only disable the streaming API if instructed to do so by LaunchDarkly support"),
                    logCapture.ToString());
                Assert.True(logCapture.HasMessageWithRegex(LogLevel.Info,
                    "^Starting LaunchDarkly PollingProcessor"),
                    logCapture.ToString());
            }
        }

        [Fact]
        public void DiagnosticStorePassedToFactories()
        {
            var epf = new Mock<IEventProcessorFactory>();
            var dsf = new Mock<IDataSourceFactory>();
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .StartWaitTime(TimeSpan.Zero)
                .Events(epf.Object)
                .DataSource(dsf.Object)
                .Build();

            IDiagnosticStore eventProcessorDiagnosticStore = null;
            IDiagnosticStore dataSourceDiagnosticStore = null;

            epf.Setup(f => f.CreateEventProcessor(It.IsAny<LdClientContext>()))
                .Callback((LdClientContext ctx) => eventProcessorDiagnosticStore = ctx.DiagnosticStore)
                .Returns(new ComponentsImpl.NullEventProcessor());
            dsf.Setup(f => f.CreateDataSource(It.IsAny<LdClientContext>(), It.IsAny<IDataSourceUpdates>()))
                .Callback((LdClientContext ctx, IDataSourceUpdates dsu) => dataSourceDiagnosticStore = ctx.DiagnosticStore)
                .Returns((LdClientContext ctx, IDataSourceUpdates dsu) => dataSource);

            using (var client = new LdClient(config))
            {
                epf.Verify(f => f.CreateEventProcessor(It.IsNotNull<LdClientContext>()), Times.Once());
                epf.VerifyNoOtherCalls();
                dsf.Verify(f => f.CreateDataSource(It.IsNotNull<LdClientContext>(), It.IsNotNull<IDataSourceUpdates>()), Times.Once());
                dsf.VerifyNoOtherCalls();
                Assert.NotNull(eventProcessorDiagnosticStore);
                Assert.Same(eventProcessorDiagnosticStore, dataSourceDiagnosticStore);
            }
        }

        [Fact]
        public void DiagnosticStoreNotPassedToFactoriesWhenOptedOut()
        {
            var epf = new Mock<IEventProcessorFactory>();
            var dsf = new Mock<IDataSourceFactory>();
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .StartWaitTime(TimeSpan.Zero)
                .Events(epf.Object)
                .DataSource(dsf.Object)
                .DiagnosticOptOut(true)
                .Build();

            IDiagnosticStore eventProcessorDiagnosticStore = null;
            IDiagnosticStore dataSourceDiagnosticStore = null;

            epf.Setup(f => f.CreateEventProcessor(It.IsAny<LdClientContext>()))
                .Callback((LdClientContext ctx) => eventProcessorDiagnosticStore = ctx.DiagnosticStore)
                .Returns(new ComponentsImpl.NullEventProcessor());
            dsf.Setup(f => f.CreateDataSource(It.IsAny<LdClientContext>(), It.IsAny<IDataSourceUpdates>()))
                .Callback((LdClientContext ctx, IDataSourceUpdates dsu) => dataSourceDiagnosticStore = ctx.DiagnosticStore)
                .Returns((LdClientContext ctx, IDataSourceUpdates dsu) => dataSource);

            using (var client = new LdClient(config))
            {
                epf.Verify(f => f.CreateEventProcessor(It.IsNotNull<LdClientContext>()), Times.Once());
                epf.VerifyNoOtherCalls();
                dsf.Verify(f => f.CreateDataSource(It.IsNotNull<LdClientContext>(), It.IsNotNull<IDataSourceUpdates>()), Times.Once());
                dsf.VerifyNoOtherCalls();
                Assert.Null(eventProcessorDiagnosticStore);
                Assert.Null(dataSourceDiagnosticStore);
            }
        }

        [Fact]
        public void NoWaitForDataSourceIfWaitMillisIsZero()
        {
            mockDataSource.Setup(up => up.Initialized()).Returns(true);
            var config = Configuration.Builder(sdkKey).StartWaitTime(TimeSpan.Zero)
                .DataSource(TestUtils.SpecificDataSource(dataSource))
                .Events(Components.NoEvents)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized());
            }
        }
        
        [Fact]
        public void DataSourceCanTimeOut()
        {
            var config = Configuration.Builder(sdkKey).StartWaitTime(TimeSpan.FromMilliseconds(10))
                .DataSource(TestUtils.SpecificDataSource(dataSource))
                .Events(Components.NoEvents)
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
            var config = Configuration.Builder(sdkKey)
                .DataSource(TestUtils.SpecificDataSource(dataSource))
                .Events(Components.NoEvents)
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

            var config = Configuration.Builder(sdkKey).StartWaitTime(TimeSpan.Zero)
                .DataStore(TestUtils.SpecificDataStore(dataStore))
                .DataSource(TestUtils.SpecificDataSource(dataSource))
                .Events(Components.NoEvents)
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

            var config = Configuration.Builder(sdkKey).StartWaitTime(TimeSpan.Zero)
                .DataStore(TestUtils.SpecificDataStore(dataStore))
                .DataSource(TestUtils.SpecificDataSource(dataSource))
                .Events(Components.NoEvents)
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

            var config = Configuration.Builder(sdkKey)
                .DataStore(TestUtils.SpecificDataStore(store))
                .DataSource(TestUtils.DataSourceWithData(DataStoreSorterTest.DependencyOrderingTestData))
                .Events(Components.NoEvents)
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