﻿using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Moq;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class PollingProcessorTest
    {
        private const string sdkKey = "SDK_KEY";
        private readonly FeatureFlag Flag = new FeatureFlagBuilder("flagkey").Build();
        private readonly Segment Segment = new Segment("segkey", 1, null, null, "", null, false);
        
        readonly Mock<IFeatureRequestor> _mockFeatureRequestor;
        readonly IFeatureRequestor _featureRequestor;
        readonly InMemoryDataStore _dataStore;
        readonly IDataStoreUpdates _dataStoreUpdates;
        readonly Configuration _config;
        readonly LdClientContext _context;

        public PollingProcessorTest()
        {
            _mockFeatureRequestor = new Mock<IFeatureRequestor>();
            _featureRequestor = _mockFeatureRequestor.Object;
            _dataStore = new InMemoryDataStore();
            _dataStoreUpdates = new DataStoreUpdates(_dataStore);
            _config = Configuration.Default(sdkKey);
            _context = new LdClientContext(new BasicConfiguration(sdkKey, false, TestUtils.NullLogger), _config);
        }

        private PollingProcessor MakeProcessor() =>
            new PollingProcessor(_context, _featureRequestor, _dataStoreUpdates,
                PollingDataSourceBuilder.DefaultPollInterval);

        [Fact]
        public void SuccessfulRequestPutsFeatureDataInStore()
        {
            AllData allData = MakeAllData();
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ReturnsAsync(allData);
            using (PollingProcessor pp = MakeProcessor())
            {
                var initTask = ((IDataSource)pp).Start();
                initTask.Wait();
                Assert.Equal(Flag, _dataStore.Get(DataKinds.Features, Flag.Key).Value.Item);
                Assert.Equal(Segment, _dataStore.Get(DataKinds.Segments, Segment.Key).Value.Item);
                Assert.True(_dataStore.Initialized());
            }
        }

        [Fact]
        public void SuccessfulRequestSetsInitializedToTrue()
        {
            AllData allData = MakeAllData();
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ReturnsAsync(allData);
            using (PollingProcessor pp = MakeProcessor())
            {
                var initTask = ((IDataSource)pp).Start();
                initTask.Wait();
                Assert.True(((IDataSource)pp).Initialized());
            }
        }

        [Fact]
        public void ConnectionErrorDoesNotCauseImmediateFailure()
        {
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ThrowsAsync(new InvalidOperationException("no"));
            using (PollingProcessor pp = MakeProcessor())
            {
                var startTime = DateTime.Now;
                var initTask = ((IDataSource)pp).Start();
                bool completed = initTask.Wait(TimeSpan.FromMilliseconds(200));
                Assert.InRange(DateTime.Now.Subtract(startTime).Milliseconds, 190, 2000);
                Assert.False(completed);
                Assert.False(((IDataSource)pp).Initialized());
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
            using (PollingProcessor pp = MakeProcessor())
            {
                var initTask = ((IDataSource)pp).Start();
                bool completed = initTask.Wait(TimeSpan.FromMilliseconds(1000));
                Assert.True(completed);
                Assert.False(((IDataSource)pp).Initialized());
            }
        }

        private void VerifyRecoverableHttpError(int status)
        {
            _mockFeatureRequestor.Setup(fr => fr.GetAllDataAsync()).ThrowsAsync(
                new UnsuccessfulResponseException(status));
            using (PollingProcessor pp = MakeProcessor())
            {
                var initTask = ((IDataSource)pp).Start();
                bool completed = initTask.Wait(TimeSpan.FromMilliseconds(200));
                Assert.False(completed);
                Assert.False(((IDataSource)pp).Initialized());
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