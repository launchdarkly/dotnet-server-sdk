using System;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class PollingProcessorTest : BaseTest
    {
        private const string sdkKey = "SDK_KEY";
        private readonly FeatureFlag Flag = new FeatureFlagBuilder("flagkey").Build();
        private readonly Segment Segment = new SegmentBuilder("segkey").Version(1).Build();
        
        readonly Mock<IFeatureRequestor> _mockFeatureRequestor;
        readonly IFeatureRequestor _featureRequestor;
        readonly InMemoryDataStore _dataStore;
        readonly IDataSourceUpdates _dataSourceUpdates;
        readonly IDataSourceStatusProvider _dataSourceStatusProvider;
        readonly Configuration _config;
        readonly LdClientContext _context;

        public PollingProcessorTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            _mockFeatureRequestor = new Mock<IFeatureRequestor>();
            _featureRequestor = _mockFeatureRequestor.Object;
            _dataStore = new InMemoryDataStore();
            var dataSourceUpdatesImpl = TestUtils.BasicDataSourceUpdates(_dataStore, testLogger);
            _dataSourceUpdates = dataSourceUpdatesImpl;
            _dataSourceStatusProvider = new DataSourceStatusProviderImpl(dataSourceUpdatesImpl);
            _config = Configuration.Default(sdkKey);
            _context = new LdClientContext(new BasicConfiguration(sdkKey, false, testLogger), _config);
        }

        private PollingProcessor MakeProcessor() =>
            new PollingProcessor(_context, _featureRequestor, _dataSourceUpdates,
                PollingDataSourceBuilder.DefaultPollInterval);

        [Fact]
        public void SuccessfulRequestPutsFeatureDataInStore()
        {
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ReturnsAsync(MakeAllData());
            using (PollingProcessor pp = MakeProcessor())
            {
                var initTask = pp.Start();
                initTask.Wait();
                Assert.Equal(Flag, _dataStore.Get(DataModel.Features, Flag.Key).Value.Item);
                Assert.Equal(Segment, _dataStore.Get(DataModel.Segments, Segment.Key).Value.Item);
                Assert.True(_dataStore.Initialized());
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
                Assert.Equal(DataSourceState.Valid, _dataSourceStatusProvider.Status.State);
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
                Assert.Equal(DataSourceState.Initializing, _dataSourceStatusProvider.Status.State);
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
                Assert.Equal(DataSourceState.Off, _dataSourceStatusProvider.Status.State);
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
            }
        }

        private FullDataSet<ItemDescriptor> MakeAllData() =>
            new DataSetBuilder().Flags(Flag).Segments(Segment).Build();
    }
}
