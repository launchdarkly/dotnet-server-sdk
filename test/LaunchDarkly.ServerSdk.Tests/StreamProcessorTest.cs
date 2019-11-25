using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaunchDarkly.Client;
using LaunchDarkly.Common;
using LaunchDarkly.EventSource;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class StreamProcessorTest
    {
        private static string SDK_KEY = "sdk_key";
        private static string FEATURE_KEY = "feature";
        private static int FEATURE_VERSION = 11;
        private static FeatureFlag FEATURE = new FeatureFlagBuilder(FEATURE_KEY).Version(FEATURE_VERSION).On(true).Build();
        private static string SEGMENT_KEY = "segment";
        private static int SEGMENT_VERSION = 22;
        private static Segment SEGMENT = new Segment(SEGMENT_KEY, SEGMENT_VERSION, null, null, null, null, false);

        Mock<IEventSource> _mockEventSource;
        IEventSource _eventSource;
        TestEventSourceFactory _eventSourceFactory;
        Mock<IFeatureRequestor> _mockRequestor;
        IFeatureRequestor _requestor;
        InMemoryDataStore _dataStore;
        Client.Configuration _config;

        public StreamProcessorTest()
        {
            _mockEventSource = new Mock<IEventSource>();
            _mockEventSource.Setup(es => es.StartAsync()).Returns(Task.CompletedTask);
            _eventSource = _mockEventSource.Object;
            _eventSourceFactory = new TestEventSourceFactory(_eventSource);
            _mockRequestor = new Mock<IFeatureRequestor>();
            _requestor = _mockRequestor.Object;
            _dataStore = new InMemoryDataStore();
            _config = Client.Configuration.Builder(SDK_KEY)
                .DataStore(TestUtils.SpecificDataStore(_dataStore))
                .Build();
        }

        [Fact]
        public void StreamUriHasCorrectEndpoint()
        {
            _config = Client.Configuration.Builder(_config).StreamUri(new Uri("http://stream.test.com")).Build();
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
        }

        [Fact]
        public void PutCausesProcessorToBeInitialized()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            Assert.True(((IDataSource)sp).Initialized());
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
            _dataStore.Upsert(VersionedDataKind.Features, FEATURE);

            string path = "/flags/" + FEATURE_KEY;
            string data = "{\"path\":\"" + path + "\",\"version\":" + (FEATURE_VERSION + 1) + "}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "delete");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            Assert.Null(_dataStore.Get(VersionedDataKind.Features, FEATURE_KEY));
        }

        [Fact]
        public void DeleteDeletesSegment()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            _dataStore.Upsert(VersionedDataKind.Segments, SEGMENT);

            string path = "/segments/" + SEGMENT_KEY;
            string data = "{\"path\":\"" + path + "\",\"version\":" + (SEGMENT_VERSION + 1) + "}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "delete");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            Assert.Null(_dataStore.Get(VersionedDataKind.Segments, SEGMENT_KEY));
        }

        [Fact]
        public void IndirectPatchRequestsAndStoresFeature()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockRequestor.Setup(r => r.GetFlagAsync(FEATURE_KEY)).ReturnsAsync(FEATURE);

            string path = "/flags/" + FEATURE_KEY;
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(path, null), "indirect/patch");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            AssertFeatureInStore(FEATURE);
        }

        [Fact]
        public void IndirectPatchRequestsAndStoresSegment()
        {
            StreamProcessor sp = CreateAndStartProcessor();
            _mockRequestor.Setup(r => r.GetSegmentAsync(SEGMENT_KEY)).ReturnsAsync(SEGMENT);

            string path = "/segments/" + SEGMENT_KEY;
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(path, null), "indirect/patch");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            AssertSegmentInStore(SEGMENT);
        }
        
        private StreamProcessor CreateProcessor()
        {
            return new StreamProcessor(_config, _requestor, _dataStore,
                _eventSourceFactory.Create());
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
            IEventSource _eventSource;

            public TestEventSourceFactory(IEventSource eventSource)
            {
                _eventSource = eventSource;
            }

            public StreamManager.EventSourceCreator Create()
            {
                return (StreamProperties sp, IDictionary<string, string> headers) =>
                {
                    ReceivedProperties = sp;
                    ReceivedHeaders = headers;
                    return _eventSource;
                };
            }
        }
        
        private void AssertFeatureInStore(FeatureFlag f)
        {
            Assert.Equal(f.Version, _dataStore.Get(VersionedDataKind.Features, f.Key).Version);
        }

        private void AssertSegmentInStore(Segment s)
        {
            Assert.Equal(s.Version, _dataStore.Get(VersionedDataKind.Segments, s.Key).Version);
        }

        private MessageReceivedEventArgs EmptyPutEvent()
        {
            string data = "{\"data\":{\"flags\":{},\"segments\":{}}}";
            return new MessageReceivedEventArgs(new MessageEvent(data, null), "put");
        }
    }
}
