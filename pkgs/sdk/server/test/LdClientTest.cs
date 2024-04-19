﻿using System;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataSources;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.Sdk.Server.Subsystems;
using LaunchDarkly.TestHelpers;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;
using static LaunchDarkly.Sdk.Server.MockComponents;

namespace LaunchDarkly.Sdk.Server
{
    // See also LDClientEvaluationTest, etc. This file contains mostly tests for the startup logic.
    public class LdClientTest : BaseTest
    {
        private static readonly ServiceEndpointsBuilder FakeEndpoints =
            Components.ServiceEndpoints().RelayProxy("http://fake");

        public LdClientTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void ClientStartupMessage()
        {
            var config = BasicConfig().Build();
            using (var client = new LdClient(config))
            {
                AssertLogMessage(true, LogLevel.Info,
                    "Starting LaunchDarkly client " + AssemblyVersions.GetAssemblyVersionStringForType(typeof(LdClient)));
                Assert.All(LogCapture.GetMessages(), m => m.LoggerName.StartsWith(LogNames.DefaultBase));
            }
        }

        [Fact]
        public void CanCustomizeBaseLoggerName()
        {
            var customLoggerName = "abcdef";
            var config = BasicConfig()
                .Logging(Components.Logging(TestLogging).BaseLoggerName(customLoggerName))
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.All(LogCapture.GetMessages(), m => m.LoggerName.StartsWith(customLoggerName));
            }
        }

        [Fact]
        public void ClientHasDefaultEventProcessorByDefault()
        {
            var config = BasicConfig()
                .DiagnosticOptOut(true)
                .Events(null) // BasicConfig sets this to NoEvents - restore the default
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<DefaultEventProcessorWrapper>(client._eventProcessor);
            }
        }

        [Fact]
        public void IsOfflineReportsFalse()
        {
            var config = BasicConfig()
                .DiagnosticOptOut(true)
                .Events(null) // BasicConfig sets this to NoEvents - restore the default
                .Build();
            using (var client = new LdClient(config))
            {
                // Just to make sure it is exposed in the interface.
                // It was not originally.
                ILdClient iClient = client;
                Assert.False(iClient.IsOffline());
            }
        }

        [Fact]
        public void DefaultDataSourceIsStreamingDataSource()
        {
            var config = BasicConfig()
                .DataSource(null) // BasicConfig sets this - restore the default
                .ServiceEndpoints(FakeEndpoints)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<StreamingDataSource>(client._dataSource);
            }
        }

        [Fact]
        public void StreamingClientHasStreamingDataSource()
        {
            var config = BasicConfig()
                .DataSource(Components.StreamingDataSource())
                .ServiceEndpoints(FakeEndpoints)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<StreamingDataSource>(client._dataSource);
            }
        }

        [Fact]
        public void StreamingClientStartupMessage()
        {
            var config = BasicConfig()
                .DataSource(Components.StreamingDataSource())
                .ServiceEndpoints(FakeEndpoints)
                .Build();
            using (var client = new LdClient(config))
            {
AssertLogMessage(false, LogLevel.Warn,
                    "You should only disable the streaming API if instructed to do so by LaunchDarkly support");
            }
        }

        [Fact]
        public void PollingClientHasPollingDataSource()
        {
            var config = BasicConfig()
                .DataSource(Components.PollingDataSource())
                .ServiceEndpoints(FakeEndpoints)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<PollingDataSource>(client._dataSource);
            }
        }

        [Fact]
        public void PollingClientStartupMessage()
        {
            var config = BasicConfig()
                .DataSource(Components.PollingDataSource())
                .ServiceEndpoints(FakeEndpoints)
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
            var epf = new Mock<IComponentConfigurer<IEventProcessor>>();
            var dsf = new Mock<IComponentConfigurer<IDataSource>>();
            var config = BasicConfig()
                .Events(epf.Object)
                .DataSource(dsf.Object)
                .Build();

            IDiagnosticStore eventProcessorDiagnosticStore = null;
            IDiagnosticStore dataSourceDiagnosticStore = null;
            var dataSource = MockDataSourceWithStartFn(_ => Task.FromResult(true));

            epf.Setup(f => f.Build(It.IsAny<LdClientContext>()))
                .Callback((LdClientContext ctx) => eventProcessorDiagnosticStore = ctx.DiagnosticStore)
                .Returns(new ComponentsImpl.NullEventProcessor());
            dsf.Setup(f => f.Build(It.IsAny<LdClientContext>()))
                .Callback((LdClientContext ctx) => dataSourceDiagnosticStore = ctx.DiagnosticStore)
                .Returns((LdClientContext ctx) => dataSource);

            using (var client = new LdClient(config))
            {
                epf.Verify(f => f.Build(It.IsNotNull<LdClientContext>()), Times.Once());
                epf.VerifyNoOtherCalls();
                dsf.Verify(f => f.Build(It.IsNotNull<LdClientContext>()), Times.Once());
                dsf.VerifyNoOtherCalls();
                Assert.NotNull(eventProcessorDiagnosticStore);
                Assert.Same(eventProcessorDiagnosticStore, dataSourceDiagnosticStore);
            }
        }

        [Fact]
        public void DiagnosticStoreNotPassedToFactoriesWhenOptedOut()
        {
            var epf = new Mock<IComponentConfigurer<IEventProcessor>>();
            var dsf = new Mock<IComponentConfigurer<IDataSource>>();
            var config = BasicConfig()
                .Events(epf.Object)
                .DataSource(dsf.Object)
                .DiagnosticOptOut(true)
                .Build();

            IDiagnosticStore eventProcessorDiagnosticStore = null;
            IDiagnosticStore dataSourceDiagnosticStore = null;
            var dataSource = MockDataSourceWithStartFn(_ => Task.FromResult(true));

            epf.Setup(f => f.Build(It.IsAny<LdClientContext>()))
                .Callback((LdClientContext ctx) => eventProcessorDiagnosticStore = ctx.DiagnosticStore)
                .Returns(new ComponentsImpl.NullEventProcessor());
            dsf.Setup(f => f.Build(It.IsAny<LdClientContext>()))
                .Callback((LdClientContext ctx) => dataSourceDiagnosticStore = ctx.DiagnosticStore)
                .Returns((LdClientContext ctx) => dataSource);

            using (var client = new LdClient(config))
            {
                epf.Verify(f => f.Build(It.IsNotNull<LdClientContext>()), Times.Once());
                epf.VerifyNoOtherCalls();
                dsf.Verify(f => f.Build(It.IsNotNull<LdClientContext>()), Times.Once());
                dsf.VerifyNoOtherCalls();
                Assert.Null(eventProcessorDiagnosticStore);
                Assert.Null(dataSourceDiagnosticStore);
            }
        }

        [Fact]
        public void DataSourceCanTimeOut()
        {
            var config = BasicConfig()
                .DataSource(MockDataSourceThatNeverStarts())
                .StartWaitTime(TimeSpan.FromMilliseconds(10))
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
            var config = BasicConfig()
                .DataSource(dataSource)
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

            var config = BasicConfig()
                .DataStore(dataStore.AsSingletonFactory<IDataStore>())
                .DataSource(MockDataSourceThatNeverStarts())
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.Equal(0, client.IntVariation("key", Context.New("user"), 0));
            }
        }

        [Fact]
        public void EvaluationUsesDataStoreIfClientIsNotInitedButStoreIsInited()
        {
            var dataStore = new InMemoryDataStore();
            dataStore.Init(FullDataSet<ItemDescriptor>.Empty());
            var flag = new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(1)).Build();
            TestUtils.UpsertFlag(dataStore, flag);

            var config = BasicConfig()
                .DataStore(dataStore.AsSingletonFactory<IDataStore>())
                .DataSource(MockDataSourceThatNeverStarts())
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.Equal(1, client.IntVariation("key", Context.New("user"), 0));
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

            var config = BasicConfig()
                .DataStore(store.AsSingletonFactory())
                .DataSource(MockDataSourceWithData(DataStoreSorterTest.DependencyOrderingTestData))
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
