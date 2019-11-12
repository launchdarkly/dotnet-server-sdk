using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.Client;
using LaunchDarkly.Common;
using Moq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class PollingProcessorTest
    {
        private readonly FeatureFlag Flag = new FeatureFlagBuilder("flagkey").Build();
        private readonly Segment Segment = new Segment("segkey", 1, null, null, "", null, false);

        Mock<IFeatureRequestor> _mockFeatureRequestor;
        IFeatureRequestor _featureRequestor;
        InMemoryFeatureStore _featureStore;
        Configuration _config;

        public PollingProcessorTest()
        {
            _mockFeatureRequestor = new Mock<IFeatureRequestor>();
            _featureRequestor = _mockFeatureRequestor.Object;
            _featureStore = TestUtils.InMemoryFeatureStore();
            _config = Configuration.Default("SDK_KEY");
        }

        [Fact]
        public void SuccessfulRequestPutsFeatureDataInStore()
        {
            AllData allData = MakeAllData();
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ReturnsAsync(allData);
            using (PollingProcessor pp = new PollingProcessor(_config, _featureRequestor, _featureStore))
            {
                var initTask = ((IUpdateProcessor)pp).Start();
                initTask.Wait();
                Assert.Equal(Flag, _featureStore.Get(VersionedDataKind.Features, Flag.Key));
                Assert.Equal(Segment, _featureStore.Get(VersionedDataKind.Segments, Segment.Key));
                Assert.True(_featureStore.Initialized());
            }
        }

        [Fact]
        public void SuccessfulRequestSetsInitializedToTrue()
        {
            AllData allData = MakeAllData();
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ReturnsAsync(allData);
            using (PollingProcessor pp = new PollingProcessor(_config, _featureRequestor, _featureStore))
            {
                var initTask = ((IUpdateProcessor)pp).Start();
                initTask.Wait();
                Assert.True(((IUpdateProcessor)pp).Initialized());
            }
        }

        [Fact]
        public void ConnectionErrorDoesNotCauseImmediateFailure()
        {
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ThrowsAsync(new InvalidOperationException("no"));
            using (PollingProcessor pp = new PollingProcessor(_config, _featureRequestor, _featureStore))
            {
                var startTime = DateTime.Now;
                var initTask = ((IUpdateProcessor)pp).Start();
                bool completed = initTask.Wait(TimeSpan.FromMilliseconds(200));
                Assert.InRange(DateTime.Now.Subtract(startTime).Milliseconds, 190, 2000);
                Assert.False(completed);
                Assert.False(((IUpdateProcessor)pp).Initialized());
            }
        }

        [Fact]
        public void HTTP401ErrorCausesImmediateFailure()
        {
            VerifyUnrecoverableHttpError(401);
        }

        [Fact]
        public void HTTP403ErrorCausesImmediateFailure()
        {
            VerifyUnrecoverableHttpError(403);
        }

        [Fact]
        public void HTTP408ErrorDoesNotCauseImmediateFailure()
        {
            VerifyRecoverableHttpError(408);
        }

        [Fact]
        public void HTTP429ErrorDoesNotCauseImmediateFailure()
        {
            VerifyRecoverableHttpError(429);
        }

        [Fact]
        public void HTTP500ErrorDoesNotCauseImmediateFailure()
        {
            VerifyRecoverableHttpError(500);
        }

        private void VerifyUnrecoverableHttpError(int status)
        {
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ThrowsAsync(
                new UnsuccessfulResponseException(status));
            using (PollingProcessor pp = new PollingProcessor(_config, _featureRequestor, _featureStore))
            {
                var initTask = ((IUpdateProcessor)pp).Start();
                bool completed = initTask.Wait(TimeSpan.FromMilliseconds(1000));
                Assert.True(completed);
                Assert.False(((IUpdateProcessor)pp).Initialized());
            }
        }

        private void VerifyRecoverableHttpError(int status)
        {
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ThrowsAsync(
                new UnsuccessfulResponseException(status));
            using (PollingProcessor pp = new PollingProcessor(_config, _featureRequestor, _featureStore))
            {
                var initTask = ((IUpdateProcessor)pp).Start();
                bool completed = initTask.Wait(TimeSpan.FromMilliseconds(200));
                Assert.False(completed);
                Assert.False(((IUpdateProcessor)pp).Initialized());
            }
        }

        private AllData MakeAllData()
        {
            IDictionary<string, FeatureFlag> flags = new Dictionary<string, FeatureFlag>();
            flags[Flag.Key] = Flag;
            IDictionary<string, Segment> segments = new Dictionary<string, Segment>();
            segments[Segment.Key] = Segment;
            return new AllData(flags, segments);
        }
    }
}
