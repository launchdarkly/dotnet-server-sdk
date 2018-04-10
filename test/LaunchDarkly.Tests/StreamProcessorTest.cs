using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaunchDarkly.Client;
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
        Mock<IFeatureRequestor> _mockRequestor;
        IFeatureRequestor _requestor;
        InMemoryFeatureStore _featureStore;
        Client.Configuration _config;

        public StreamProcessorTest()
        {
            _mockEventSource = new Mock<IEventSource>();
            _mockEventSource.Setup(es => es.StartAsync()).Returns(Task.CompletedTask);
            _eventSource = _mockEventSource.Object;
            _mockRequestor = new Mock<IFeatureRequestor>();
            _requestor = _mockRequestor.Object;
            _featureStore = new InMemoryFeatureStore();
            _config = Client.Configuration.Default(SDK_KEY).WithFeatureStore(_featureStore);
        }

        [Fact]
        public void StreamUriHasCorrectEndpoint()
        {
            _config = _config.WithStreamUri(new Uri("http://stream.test.com"));
            TestStreamProcessor sp = CreateAndStartProcessor();
            Assert.Equal(new Uri("http://stream.test.com/all"), sp.ActualStreamUri);
        }

        [Fact]
        public void HeadersHaveAuthorization()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            Assert.Equal(SDK_KEY, sp.Headers["Authorization"]);
        }

        [Fact]
        public void HeadersHaveUserAgent()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            Assert.Equal("DotNetClient/" + Client.Configuration.Version, sp.Headers["User-Agent"]);
        }

        [Fact]
        public void HeadersHaveAccept()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            Assert.Equal("text/event-stream", sp.Headers["Accept"]);
        }

        [Fact]
        public void PutCausesFeatureToBeStored()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            string data = "{\"data\":{\"flags\":{\"" +
                FEATURE_KEY + "\":" + JsonConvert.SerializeObject(FEATURE) + "},\"segments\":{}}}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "put");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            AssertFeatureInStore(FEATURE);
        }

        [Fact]
        public void PutCausesSegmentToBeStored()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            string data = "{\"data\":{\"flags\":{},\"segments\":{\"" +
                SEGMENT_KEY + "\":" + JsonConvert.SerializeObject(SEGMENT) + "}}}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "put");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            AssertSegmentInStore(SEGMENT);
        }

        [Fact]
        public void StoreNotInitializedByDefault()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            Assert.False(_featureStore.Initialized());
        }

        [Fact]
        public void PutCausesStoreToBeInitialized()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            Assert.True(_featureStore.Initialized());
        }

        [Fact]
        public void ProcessorNotInitializedByDefault()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            Assert.False(((IUpdateProcessor)sp).Initialized());
        }

        [Fact]
        public void PutCausesProcessorToBeInitialized()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            Assert.True(((IUpdateProcessor)sp).Initialized());
        }

        [Fact]
        public void TaskIsNotCompletedByDefault()
        {
            TestStreamProcessor sp = CreateProcessor();
            Task<bool> task = ((IUpdateProcessor)sp).Start();
            Assert.False(task.IsCompleted);
        }
        
        [Fact]
        public void PutCausesTaskToBeCompleted()
        {
            TestStreamProcessor sp = CreateProcessor();
            Task<bool> task = ((IUpdateProcessor)sp).Start();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void PatchUpdatesFeature()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
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
            TestStreamProcessor sp = CreateAndStartProcessor();
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
            TestStreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            _featureStore.Upsert(VersionedDataKind.Features, FEATURE);

            string path = "/flags/" + FEATURE_KEY;
            string data = "{\"path\":\"" + path + "\",\"version\":" + (FEATURE_VERSION + 1) + "}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "delete");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            Assert.Null(_featureStore.Get(VersionedDataKind.Features, FEATURE_KEY));
        }

        [Fact]
        public void DeleteDeletesSegment()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            _mockEventSource.Raise(es => es.MessageReceived += null, EmptyPutEvent());
            _featureStore.Upsert(VersionedDataKind.Segments, SEGMENT);

            string path = "/segments/" + SEGMENT_KEY;
            string data = "{\"path\":\"" + path + "\",\"version\":" + (SEGMENT_VERSION + 1) + "}";
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(data, null), "delete");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            Assert.Null(_featureStore.Get(VersionedDataKind.Segments, SEGMENT_KEY));
        }

        [Fact]
        public void IndirectPatchRequestsAndStoresFeature()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            _mockRequestor.Setup(r => r.GetFlagAsync(FEATURE_KEY)).ReturnsAsync(FEATURE);

            string path = "/flags/" + FEATURE_KEY;
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(path, null), "indirect/patch");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            AssertFeatureInStore(FEATURE);
        }

        [Fact]
        public void IndirectPatchRequestsAndStoresSegment()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            _mockRequestor.Setup(r => r.GetSegmentAsync(SEGMENT_KEY)).ReturnsAsync(SEGMENT);

            string path = "/segments/" + SEGMENT_KEY;
            MessageReceivedEventArgs e = new MessageReceivedEventArgs(new MessageEvent(path, null), "indirect/patch");
            _mockEventSource.Raise(es => es.MessageReceived += null, e);

            AssertSegmentInStore(SEGMENT);
        }
        
        [Fact]
        public void GeneralExceptionDoesNotStopStream()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            ExceptionEventArgs e = new ExceptionEventArgs(new Exception("whatever"));
            _mockEventSource.Raise(es => es.Error += null, e);

            _mockEventSource.Verify(es => es.Close(), Times.Never());
        }

        [Fact]
        public void Http500ErrorDoesNotStopStream()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            ExceptionEventArgs e = new ExceptionEventArgs(new EventSourceServiceUnsuccessfulResponseException("", 500));
            _mockEventSource.Raise(es => es.Error += null, e);

            _mockEventSource.Verify(es => es.Close(), Times.Never());
        }

        [Fact]
        public void Http401ErrorStopsStream()
        {
            TestStreamProcessor sp = CreateAndStartProcessor();
            ExceptionEventArgs e = new ExceptionEventArgs(new EventSourceServiceUnsuccessfulResponseException("", 401));
            _mockEventSource.Raise(es => es.Error += null, e);

            _mockEventSource.Verify(es => es.Close());
        }

        private TestStreamProcessor CreateProcessor()
        {
            return new TestStreamProcessor(_config, _requestor, _featureStore, _eventSource);
        }

        private TestStreamProcessor CreateAndStartProcessor()
        {
            TestStreamProcessor sp = CreateProcessor();
            ((IUpdateProcessor)sp).Start();
            return sp;
        }

        class TestStreamProcessor : StreamProcessor
        {
            public Uri ActualStreamUri { get; private set; }
            public Dictionary<string, string> Headers { get; private set; }
            IEventSource _eventSource;

            public TestStreamProcessor(Client.Configuration config, IFeatureRequestor featureRequestor, IFeatureStore featureStore, IEventSource eventSource) :
                base(config, featureRequestor, featureStore)
            {
                _eventSource = eventSource;
            }

            override protected IEventSource CreateEventSource(Uri streamUri, Dictionary<string, string> headers)
            {
                ActualStreamUri = streamUri;
                Headers = headers;
                return _eventSource;
            }
        }
        
        private void AssertFeatureInStore(FeatureFlag f)
        {
            Assert.Equal(f.Version, _featureStore.Get(VersionedDataKind.Features, f.Key).Version);
        }

        private void AssertSegmentInStore(Segment s)
        {
            Assert.Equal(s.Version, _featureStore.Get(VersionedDataKind.Segments, s.Key).Version);
        }

        private MessageReceivedEventArgs EmptyPutEvent()
        {
            string data = "{\"data\":{\"flags\":{},\"segments\":{}}}";
            return new MessageReceivedEventArgs(new MessageEvent(data, null), "put");
        }
    }
}
