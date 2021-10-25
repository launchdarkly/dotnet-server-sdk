using System;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class PollingProcessorTest : BaseTest
    {
        private readonly FeatureFlag Flag = new FeatureFlagBuilder("flagkey").Build();
        private readonly Segment Segment = new SegmentBuilder("segkey").Version(1).Build();
        
        readonly Mock<IFeatureRequestor> _mockFeatureRequestor;
        readonly IFeatureRequestor _featureRequestor;
        readonly CapturingDataSourceUpdates _updates = new CapturingDataSourceUpdates();

        public PollingProcessorTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            _mockFeatureRequestor = new Mock<IFeatureRequestor>();
            _featureRequestor = _mockFeatureRequestor.Object;
        }

        private PollingProcessor MakeProcessor() =>
            new PollingProcessor(BasicContext, _featureRequestor, _updates,
                PollingDataSourceBuilder.DefaultPollInterval);

        [Fact]
        public void SuccessfulRequestPutsFeatureDataInStore()
        {
            var expectedData = MakeAllData();
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ReturnsAsync(expectedData);

            using (PollingProcessor pp = MakeProcessor())
            {
                var initTask = pp.Start();
                initTask.Wait();

                var receivedData = _updates.Inits.ExpectValue();
                AssertHelpers.DataSetsEqual(expectedData, receivedData);
            }
        }

        [Fact]
        public void SuccessfulRequestSetsInitializedToTrue()
        {
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ReturnsAsync(MakeAllData());

            using (PollingProcessor pp = MakeProcessor())
            {
                var initTask = pp.Start();
                initTask.Wait();

                Assert.True(pp.Initialized);

                var receivedStatus = _updates.StatusUpdates.ExpectValue();
                Assert.Equal(DataSourceState.Valid, receivedStatus.State);
            }
        }

        [Fact]
        public void ConnectionErrorDoesNotCauseImmediateFailure()
        {
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ThrowsAsync(new InvalidOperationException("no"));

            using (PollingProcessor pp = MakeProcessor())
            {
                var initTask = pp.Start();
                bool completed = initTask.Wait(TimeSpan.FromMilliseconds(200));
                Assert.False(completed);
                Assert.False(pp.Initialized);

                var receivedStatus = _updates.StatusUpdates.ExpectValue();
                Assert.Equal(DataSourceState.Interrupted, receivedStatus.State);
            }
        }

        [Theory]
        [InlineData(401)]
        [InlineData(403)]
        public void VerifyUnrecoverableHttpError(int status)
        {
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ThrowsAsync(
                new UnsuccessfulResponseException(status));

            using (PollingProcessor pp = MakeProcessor())
            {
                var initTask = pp.Start();
                bool completed = initTask.Wait(TimeSpan.FromMilliseconds(1000));
                Assert.True(completed);
                Assert.False(pp.Initialized);

                var receivedStatus = _updates.StatusUpdates.ExpectValue();
                Assert.Equal(DataSourceState.Off, receivedStatus.State);
                Assert.NotNull(receivedStatus.Error);
                Assert.Equal(status, receivedStatus.Error.Value.StatusCode);
            }
        }

        [Theory]
        [InlineData(408)]
        [InlineData(429)]
        [InlineData(500)]
        public void VerifyRecoverableHttpError(int status)
        {
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ThrowsAsync(
                new UnsuccessfulResponseException(status));

            using (PollingProcessor pp = MakeProcessor())
            {
                var initTask = pp.Start();
                bool completed = initTask.Wait(TimeSpan.FromMilliseconds(200));
                Assert.False(completed);
                Assert.False(pp.Initialized);

                var receivedStatus = _updates.StatusUpdates.ExpectValue();
                Assert.Equal(DataSourceState.Interrupted, receivedStatus.State);
                Assert.NotNull(receivedStatus.Error);
                Assert.Equal(status, receivedStatus.Error.Value.StatusCode);
            }
        }

        private FullDataSet<ItemDescriptor> MakeAllData() =>
            new DataSetBuilder().Flags(Flag).Segments(Segment).Build();
    }
}
