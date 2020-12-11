using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Internal.Stream;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.EventSource;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class StreamProcessorTest : BaseTest
    {
        private const string SDK_KEY = "sdk_key";
        private const string FEATURE_KEY = "feature";
        private const int FEATURE_VERSION = 11;
        private static readonly FeatureFlag FEATURE = new FeatureFlagBuilder(FEATURE_KEY).Version(FEATURE_VERSION).On(true).Build();
        private const string SEGMENT_KEY = "segment";
        private const int SEGMENT_VERSION = 22;
        private static readonly Segment SEGMENT = new Segment(SEGMENT_KEY, SEGMENT_VERSION, null, null, null, null, false);

        readonly Mock<IEventSource> _mockEventSource;
        readonly IEventSource _eventSource;
        readonly TestEventSourceFactory _eventSourceFactory;
        readonly InMemoryDataStore _dataStore;
        readonly IDataSourceUpdates _dataSourceUpdates;
        readonly IDataSourceStatusProvider _dataSourceStatusProvider;
        Configuration _config;

        public StreamProcessorTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            _mockEventSource = new Mock<IEventSource>();
            _mockEventSource.Setup(es => es.StartAsync()).Returns(Task.CompletedTask);
            _eventSource = _mockEventSource.Object;
            _eventSourceFactory = new TestEventSourceFactory(_eventSource);
            _dataStore = new InMemoryDataStore();
            var dataSourceUpdatesImpl = new DataSourceUpdatesImpl(_dataStore,
                new TaskExecutor(testLogger),
                testLogger, null);
            _dataSourceUpdates = dataSourceUpdatesImpl;
            _dataSourceStatusProvider = new DataSourceStatusProviderImpl(dataSourceUpdatesImpl);
            _config = Configuration.Builder(SDK_KEY)
                .DataSource(Components.StreamingDataSource().EventSourceCreator(_eventSourceFactory.Create()))
                .DataStore(TestUtils.SpecificDataStore(_dataStore))
                .Logging(Components.Logging(testLogging))
                .Build();
        }

        [Fact]
        public void StreamUriHasCorrectEndpoint()
        {
            _config = Server.Configuration.Builder(_config)
                .DataSource(
                    Components.StreamingDataSource()
                        .BaseUri(new Uri("http://stream.test.com"))
                        .EventSourceCreator(_eventSourceFactory.Create())
                    )
                .Build();
            StreamProcessor sp = CreateAndStartProcessor();
            Assert.Equal(new Uri("http://stream.test.com/all"),
                _eventSourceFactory.ReceivedProperties.StreamUri);
        }
        
        [Fact]
        public void PutCausesFeatureToBeStored()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            string data = "{\"data\":{\"flags\":{\"" +
                FEATURE_KEY + "\":" + JsonConvert.SerializeObject(FEATURE) + "},\"segments\":{}}}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "put");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            AssertFeatureInStore(FEATURE);
        }

        [Fact]
        public void PutCausesSegmentToBeStored()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            string data = "{\"data\":{\"flags\":{},\"segments\":{\"" +
                SEGMENT_KEY + "\":" + JsonConvert.SerializeObject(SEGMENT) + "}}}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "put");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            AssertSegmentInStore(SEGMENT);
        }

        [Fact]
        public void StoreNotInitializedByDefault()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            Assert.False(_dataStore.Initialized());
        }

        [Fact]
        public void PutCausesStoreToBeInitialized()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            Assert.True(_dataStore.Initialized());
        }

        [Fact]
        public void ProcessorNotInitializedByDefault()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            Assert.False(((IDataSource)sp).Initialized());
            Assert.Equal(DataSourceState.Initializing, _dataSourceStatusProvider.Status.State);
        }

        [Fact]
        public void PutCausesProcessorToBeInitialized()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            Assert.True(((IDataSource)sp).Initialized());
            Assert.Equal(DataSourceState.Valid, _dataSourceStatusProvider.Status.State);
        }

        [Fact]
        public void TaskIsNotCompletedByDefault()
        {
            StreamProcessor sp = CreateProcessor();
            Task<bool> task = ((IDataSource)sp).Start();
            Assert.False(task.IsCompleted);
        }
        
        [Fact]
        public void PutCausesTaskToBeCompleted()
        {
            StreamProcessor sp = CreateProcessor();
            Task<bool> task = ((IDataSource)sp).Start();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void PatchUpdatesFeature()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());

            string path = "/flags/" + FEATURE_KEY;
            string data = "{\"path\":\"" + path + "\",\"data\":" + JsonConvert.SerializeObject(FEATURE) + "}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "patch");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            AssertFeatureInStore(FEATURE);
        }
        
        [Fact]
        public void PatchUpdatesSegment()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());

            string path = "/segments/" + SEGMENT_KEY;
            string data = "{\"path\":\"" + path + "\",\"data\":" + JsonConvert.SerializeObject(SEGMENT) + "}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "patch");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            AssertSegmentInStore(SEGMENT);
        }

        [Fact]
        public void DeleteDeletesFeature()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            TestUtils.UpsertFlag(_dataStore, FEATURE);

            string path = "/flags/" + FEATURE_KEY;
            int deletedVersion = FEATURE.Version + 1;
            string data = "{\"path\":\"" + path + "\",\"version\":" + deletedVersion + "}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "delete");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            Assert.Equal(ItemDescriptor.Deleted(deletedVersion),
                _dataStore.Get(DataKinds.Features, FEATURE_KEY));
        }

        [Fact]
        public void DeleteDeletesSegment()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            TestUtils.UpsertSegment(_dataStore, SEGMENT);

            string path = "/segments/" + SEGMENT_KEY;
            int deletedVersion = SEGMENT.Version + 1;
            string data = "{\"path\":\"" + path + "\",\"version\":" + deletedVersion + "}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "delete");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            Assert.Equal(ItemDescriptor.Deleted(deletedVersion),
                _dataStore.Get(DataKinds.Segments, SEGMENT_KEY));
        }

        [Fact]
        public void RecoverableErrorChangesStateToInterrupted()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            Assert.True(((IDataSource)sp).Initialized());
            Assert.Equal(DataSourceState.Valid, _dataSourceStatusProvider.Status.State);

            var ex = new EventSourceServiceUnsuccessfulResponseException("", 500);
            _mockEventSource.Raise(es => es.Error += null, new EventSource.ExceptionEventArgs(ex));

            var newStatus = _dataSourceStatusProvider.Status;
            Assert.Equal(DataSourceState.Interrupted, newStatus.State);
            Assert.NotNull(newStatus.LastError);
            Assert.Equal(DataSourceStatus.ErrorKind.ErrorResponse, newStatus.LastError.Value.Kind);
            Assert.Equal(500, newStatus.LastError.Value.StatusCode);
        }

        [Fact]
        public void UnrecoverableErrorChangesStateToOff()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            Assert.True(((IDataSource)sp).Initialized());
            Assert.Equal(DataSourceState.Valid, _dataSourceStatusProvider.Status.State);

            var ex = new EventSourceServiceUnsuccessfulResponseException("", 401);
            _mockEventSource.Raise(es => es.Error += null, new EventSource.ExceptionEventArgs(ex));

            var newStatus = _dataSourceStatusProvider.Status;
            Assert.Equal(DataSourceState.Off, newStatus.State);
            Assert.NotNull(newStatus.LastError);
            Assert.Equal(DataSourceStatus.ErrorKind.ErrorResponse, newStatus.LastError.Value.Kind);
            Assert.Equal(401, newStatus.LastError.Value.StatusCode);
        }

        private StreamProcessor CreateProcessor()
        {
            var basicConfig = new BasicConfiguration(SDK_KEY, false, testLogger);
            return _config.DataSourceFactory.CreateDataSource(
                new LdClientContext(basicConfig, _config),
                _dataSourceUpdates
                ) as StreamProcessor;
        }

        private StreamProcessor CreateAndStartProcessor()
        {
            StreamProcessor sp = CreateProcessor();
            ((IDataSource)sp).Start();
            return sp;
        }

        class TestEventSourceFactory
        {
            public StreamProperties ReceivedProperties { get; private set; }
            public IDictionary<string, string> ReceivedHeaders { get; private set; }
            readonly IEventSource _eventSource;

            public TestEventSourceFactory(IEventSource eventSource)
            {
                _eventSource = eventSource;
            }

            public StreamManager.EventSourceCreator Create()
            {
                return (StreamProperties sp, HttpProperties hp) =>
                {
                    ReceivedProperties = sp;
                    ReceivedHeaders = hp.BaseHeaders.ToDictionary(kv => kv.Key, kv => kv.Value);
                    return _eventSource;
                };
            }
        }
        
        private void AssertFeatureInStore(FeatureFlag f)
        {
            Assert.Equal(f.Version, _dataStore.Get(DataKinds.Features, f.Key).Value.Version);
        }

        private void AssertSegmentInStore(Segment s)
        {
            Assert.Equal(s.Version, _dataStore.Get(DataKinds.Segments, s.Key).Value.Version);
        }

        private MessageReceivedEventArgs EmptyPutEvent()
        {
            string data = "{\"data\":{\"flags\":{},\"segments\":{}}}";
            return new MessageReceivedEventArgs(new MessageEvent(data, null), "put");
        }
    }
}
