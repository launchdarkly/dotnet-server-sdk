using System;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataSources;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.TestHelpers;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using static LaunchDarkly.Sdk.Server.MockComponents;

namespace LaunchDarkly.Sdk.Server
{
    // See also LDClientEvaluationTest, etc. This file contains mostly tests for the startup logic.
    public class LdClientTest : BaseTest
    {
        private const string sdkKey = "SDK_KEY";

        public LdClientTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void ClientStartupMessage()
        {
            var config = Configuration.Builder(sdkKey)
                .Logging(Components.Logging(testLogging))
                .Events(Components.NoEvents)
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                AssertLogMessage(true, LogLevel.Info,
                    "Starting LaunchDarkly client " + AssemblyVersions.GetAssemblyVersionStringForType(typeof(LdClient)));
                Assert.All(logCapture.GetMessages(), m => m.LoggerName.StartsWith(LogNames.DefaultBase));
            }
        }

        [Fact]
        public void CanCustomizeBaseLoggerName()
        {
            var customLoggerName = "abcdef";
            var config = Configuration.Builder(sdkKey)
                .Logging(Components.Logging(testLogging).BaseLoggerName(customLoggerName))
                .Events(Components.NoEvents)
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.All(logCapture.GetMessages(), m => m.LoggerName.StartsWith(customLoggerName));
            }
        }

        [Fact]
        public void ClientHasDefaultEventProcessorByDefault()
        {
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .StartWaitTime(TimeSpan.Zero)
                .DiagnosticOptOut(true)
                .Logging(Components.Logging(testLogging))
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
                .Logging(Components.Logging(testLogging))
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<StreamProcessor>(client._dataSource);
            }
        }

        [Fact]
        public void StreamingClientStartupMessage()
        {
            var config = Configuration.Builder(sdkKey)
                .Logging(Components.Logging(testLogging))
                .DataSource(Components.StreamingDataSource().BaseUri(new Uri("http://fake")))
                .Events(Components.NoEvents)
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                AssertLogMessage(false, LogLevel.Warn,
                    "You should only disable the streaming API if instructed to do so by LaunchDarkly support");
            }
        }

        [Fact]
        public void PollingClientHasPollingProcessor()
        {
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.PollingDataSource().BaseUri(new Uri("http://fake")))
                .Events(Components.NoEvents)
                .StartWaitTime(TimeSpan.Zero)
                .Logging(Components.Logging(testLogging))
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<PollingProcessor>(client._dataSource);
            }
        }

        [Fact]
        public void PollingClientStartupMessage()
        {
            var config = Configuration.Builder(sdkKey)
                .Logging(Components.Logging(testLogging))
                .Events(Components.NoEvents)
                .DataSource(Components.PollingDataSource().BaseUri(new Uri("http://fake")))
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            using (var client = new LdClient(config))
            {
                AssertLogMessageRegex(true, LogLevel.Warn,
                    "You should only disable the streaming API if instructed to do so by LaunchDarkly support");
                AssertLogMessageRegex(true, LogLevel.Info, "^Starting LaunchDarkly polling");
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
                .Logging(Components.Logging(testLogging))
                .Build();

            IDiagnosticStore eventProcessorDiagnosticStore = null;
            IDiagnosticStore dataSourceDiagnosticStore = null;
            var dataSource = MockDataSourceWithStartFn(_ => Task.FromResult(true));

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
                .Logging(Components.Logging(testLogging))
                .Build();

            IDiagnosticStore eventProcessorDiagnosticStore = null;
            IDiagnosticStore dataSourceDiagnosticStore = null;
            var dataSource = MockDataSourceWithStartFn(_ => Task.FromResult(true));

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
            var config = Configuration.Builder(sdkKey).StartWaitTime(TimeSpan.Zero)
                .DataSource(MockDataSourceThatNeverStarts().AsSingletonFactory())
                .Events(Components.NoEvents)
                .Logging(Components.Logging(testLogging))
                .Build();

            using (var client = new LdClient(config))
            {
            }
        }
        
        [Fact]
        public void DataSourceCanTimeOut()
        {
            var config = Configuration.Builder(sdkKey).StartWaitTime(TimeSpan.FromMilliseconds(10))
                .DataSource(MockDataSourceThatNeverStarts().AsSingletonFactory())
                .Events(Components.NoEvents)
                .Logging(Components.Logging(testLogging))
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.False(client.Initialized);
            }
        }

        [Fact]
        public void ExceptionFromDataSourceTaskDoesNotCauseExceptionInInit()
        {
            TaskCompletionSource<bool> errorTaskSource = new TaskCompletionSource<bool>();
            var dataSource = MockDataSourceWithStartFn(_ => errorTaskSource.Task, () => false);
            errorTaskSource.SetException(new Exception("bad"));
            var config = Configuration.Builder(sdkKey)
                .DataSource(dataSource.AsSingletonFactory())
                .Events(Components.NoEvents)
                .Logging(Components.Logging(testLogging))
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.False(client.Initialized);
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
                .DataStore(dataStore.AsSingletonFactory())
                .DataSource(MockDataSourceThatNeverStarts().AsSingletonFactory())
                .Events(Components.NoEvents)
                .Logging(Components.Logging(testLogging))
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
                .DataStore(dataStore.AsSingletonFactory())
                .DataSource(MockDataSourceThatNeverStarts().AsSingletonFactory())
                .Events(Components.NoEvents)
                .Logging(Components.Logging(testLogging))
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
            var dataSink = new EventSink<FullDataSet<ItemDescriptor>>();

            mockStore.Setup(s => s.Init(It.IsAny<FullDataSet<ItemDescriptor>>()))
                .Callback((FullDataSet<ItemDescriptor> data) => dataSink.Enqueue(data));

            var config = Configuration.Builder(sdkKey)
                .DataStore(store.AsSingletonFactory())
                .DataSource(MockDataSourceWithData(DataStoreSorterTest.DependencyOrderingTestData).AsSingletonFactory())
                .Events(Components.NoEvents)
                .Logging(Components.Logging(testLogging))
                .Build();

            using (var client = new LdClient(config))
            {
                var receivedData = dataSink.ExpectValue();
                DataStoreSorterTest.VerifyDataSetOrder(receivedData, DataStoreSorterTest.DependencyOrderingTestData,
                    DataStoreSorterTest.ExpectedOrderingForSortedDataSet);
            }
        }
    }
}