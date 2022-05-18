using System;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.TestHelpers;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.BigSegments.BigSegmentsInternalTypes;

namespace LaunchDarkly.Sdk.Server.Internal.BigSegments
{
    public class BigSegmentsStoreWrapperTest : BaseTest
    {
        private readonly MockBigSegmentStore _store;
        private readonly IComponentConfigurer<IBigSegmentStore> _storeFactory;

        public BigSegmentsStoreWrapperTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            _store = new MockBigSegmentStore();
            _storeFactory = _store.AsSingletonFactory<IBigSegmentStore>();
        }

        private void SetStoreHasNoMetadata() => _store.SetupMetadataReturns((StoreMetadata?)null);
        
        private void SetStoreTimestamp(UnixMillisecondTime timestamp) =>
            _store.SetupMetadataReturns(new StoreMetadata { LastUpToDate = timestamp });

        private void SetStoreStatusError(Exception e) =>
            _store.SetupMetadataThrows(e);

        private void SetStoreMembership(string userKey, IMembership membership) =>
            _store.SetupMembershipReturns(BigSegmentContextKeyHash(userKey), membership);

        private void ShouldHaveQueriedMembershipTimes(string userKey, int times) =>
            Assert.Equal(times, _store.InspectMembershipQueriedCount(BigSegmentContextKeyHash(userKey)));

        [Fact]
        public void MembershipQueryWithUncachedResultAndHealthyStatus()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now);

            var expectedMembership = NewMembershipFromSegmentRefs(new string[] { "key1" }, new string[] { "key2 " });
            var userKey = "userkey";
            SetStoreMembership(userKey, expectedMembership);

            var bsConfig = Components.BigSegments(_storeFactory)
                .StaleAfter(TimeSpan.FromDays(1))
                .Build(BasicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, BasicTaskExecutor, TestLogger))
            {
                var result = sw.GetMembership(userKey);
                Assert.Equal(expectedMembership, result.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result.Status);
            }
        }

        [Fact]
        public void MembershipQueryWithCachedResultAndHealthyStatus()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now);

            var expectedMembership = NewMembershipFromSegmentRefs(new string[] { "key1" }, new string[] { "key2 " });
            var userKey = "userkey";
            SetStoreMembership(userKey, expectedMembership);

            var bsConfig = Components.BigSegments(_storeFactory)
                .StaleAfter(TimeSpan.FromDays(1))
                .Build(BasicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, BasicTaskExecutor, TestLogger))
            {
                var result1 = sw.GetMembership(userKey);
                Assert.Equal(expectedMembership, result1.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result1.Status);

                var result2 = sw.GetMembership(userKey);
                Assert.Equal(expectedMembership, result2.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result2.Status);

                ShouldHaveQueriedMembershipTimes(userKey, 1);
            }
        }

        [Fact]
        public void MembershipQueryWithStaleStatus()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now.PlusMillis(-1000));

            var expectedMembership = NewMembershipFromSegmentRefs(new string[] { "key1" }, new string[] { "key2 " });
            var userKey = "userkey";
            SetStoreMembership(userKey, expectedMembership);

            var bsConfig = Components.BigSegments(_storeFactory)
                .StaleAfter(TimeSpan.FromMilliseconds(500))
                .Build(BasicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, BasicTaskExecutor, TestLogger))
            {
                var result = sw.GetMembership(userKey);
                Assert.Equal(expectedMembership, result.Membership);
                Assert.Equal(BigSegmentsStatus.Stale, result.Status);
            }
        }

        [Fact]
        public void MembershipQueryWithStaleStatusDueToNoStoreMetadata()
        {
            SetStoreHasNoMetadata();

            var expectedMembership = NewMembershipFromSegmentRefs(new string[] { "key1" }, new string[] { "key2 " });
            var userKey = "userkey";
            SetStoreMembership(userKey, expectedMembership);

            var bsConfig = Components.BigSegments(_storeFactory)
                .StaleAfter(TimeSpan.FromMilliseconds(500))
                .Build(BasicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, BasicTaskExecutor, TestLogger))
            {
                var result = sw.GetMembership(userKey);
                Assert.Equal(expectedMembership, result.Membership);
                Assert.Equal(BigSegmentsStatus.Stale, result.Status);
            }
        }

        [Fact]
        public void LeastRecentUserIsEvictedFromCache()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now);

            const string userKey1 = "userkey1", userKey2 = "userkey2", userKey3 = "userkey3";
            IMembership expectedMembership1 = NewMembershipFromSegmentRefs(new string[] { "seg1" }, null),
                expectedMembership2 = NewMembershipFromSegmentRefs(new string[] { "seg2" }, null),
                expectedMembership3 = NewMembershipFromSegmentRefs(new string[] { "seg3" }, null);
            SetStoreMembership(userKey1, expectedMembership1);
            SetStoreMembership(userKey2, expectedMembership2);
            SetStoreMembership(userKey3, expectedMembership3);

            var bsConfig = Components.BigSegments(_storeFactory)
                .ContextCacheSize(2)
                .StaleAfter(TimeSpan.FromDays(1))
                .Build(BasicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, BasicTaskExecutor, TestLogger))
            {
                var result1 = sw.GetMembership(userKey1);
                Assert.Equal(expectedMembership1, result1.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result1.Status);

                var result2 = sw.GetMembership(userKey2);
                Assert.Equal(expectedMembership2, result2.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result2.Status);

                var result3 = sw.GetMembership(userKey3);
                Assert.Equal(expectedMembership3, result3.Membership);
                Assert.Equal(BigSegmentsStatus.Healthy, result3.Status);

                ShouldHaveQueriedMembershipTimes(userKey1, 1);
                ShouldHaveQueriedMembershipTimes(userKey2, 1);
                ShouldHaveQueriedMembershipTimes(userKey3, 1);

                // Since the capacity is only 2 and userKey1 was the least recently used, that key should be
                // evicted by the userKey3 query. Now only userKey2 and userKey3 are in the cache, and
                // querying them again should not cause a new query to the store.

                var result2a = sw.GetMembership(userKey2);
                Assert.Equal(expectedMembership2, result2a.Membership);
                var result3a = sw.GetMembership(userKey3);
                Assert.Equal(expectedMembership3, result3a.Membership);

                ShouldHaveQueriedMembershipTimes(userKey1, 1);
                ShouldHaveQueriedMembershipTimes(userKey2, 1);
                ShouldHaveQueriedMembershipTimes(userKey3, 1);

                var result1a = sw.GetMembership(userKey1);
                Assert.Equal(expectedMembership1, result1a.Membership);

                ShouldHaveQueriedMembershipTimes(userKey1, 2);
                ShouldHaveQueriedMembershipTimes(userKey2, 1);
                ShouldHaveQueriedMembershipTimes(userKey3, 1);
            }
        }

        [Fact]
        public void PollingDetectsStoreUnavailability()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now);

            var bsConfig = Components.BigSegments(_storeFactory)
                .StatusPollInterval(TimeSpan.FromMilliseconds(10))
                .StaleAfter(TimeSpan.FromDays(1))
                .Build(BasicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, BasicTaskExecutor, TestLogger))
            {
                var status1 = sw.GetStatus();
                Assert.True(status1.Available);

                var statuses = new EventSink<BigSegmentStoreStatus>();
                sw.StatusChanged += statuses.Add;

                SetStoreStatusError(new Exception("sorry"));

                var status2 = statuses.ExpectValue();
                if (status2.Available)
                {
                    // depending on timing, we might or might not receive an initial update of Available = true
                    status2 = statuses.ExpectValue();
                    Assert.False(status2.Available);
                }
                Assert.Equal(status2, sw.GetStatus());

                SetStoreTimestamp(UnixMillisecondTime.Now);

                var status3 = statuses.ExpectValue();
                Assert.True(status3.Available);
                Assert.Equal(status3, sw.GetStatus());
            }

            AssertLogMessageRegex(true, Logging.LogLevel.Error,
                "Big Segment store status.*Exception.*sorry");
        }

        [Fact]
        public void PollingDetectsStaleStatus()
        {
            SetStoreTimestamp(UnixMillisecondTime.Now.PlusMillis(5000)); // future time, definitely not stale

            var bsConfig = Components.BigSegments(_storeFactory)
                .StatusPollInterval(TimeSpan.FromMilliseconds(10))
                .StaleAfter(TimeSpan.FromMilliseconds(200))
                .Build(BasicContext);
            using (var sw = new BigSegmentStoreWrapper(bsConfig, BasicTaskExecutor, TestLogger))
            {
                var status1 = sw.GetStatus();
                Assert.False(status1.Stale);

                var statuses = new EventSink<BigSegmentStoreStatus>();
                sw.StatusChanged += statuses.Add;

                SetStoreTimestamp(UnixMillisecondTime.Now.PlusMillis(-200));

                var status2 = statuses.ExpectValue();
                Assert.True(status2.Stale);

                SetStoreTimestamp(UnixMillisecondTime.Now.PlusMillis(5000));

                var status3 = statuses.ExpectValue();
                Assert.False(status3.Stale);
            }
        }
    }
}
