﻿using System;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.Sdk.Server.Subsystems;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Subsystems.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.BigSegments.BigSegmentsInternalTypes;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientBigSegmentsTest : BaseTest
    {
        private TestData _testData;
        private FeatureFlag _flag;
        private Segment _bigSegment;
        private Context _user;
        private MockBigSegmentStore _storeMock;
        private IComponentConfigurer<IBigSegmentStore> _storeFactory;

        public LdClientBigSegmentsTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            _testData = TestData.DataSource();

            _user = Context.New("userkey");
            _bigSegment = new SegmentBuilder("segmentkey")
                .Unbounded(true)
                .Generation(1)
                .Build();
            _flag = new FeatureFlagBuilder("flagkey")
                .On(true)
                .Variations(false, true)
                .FallthroughVariation(0)
                .Rules(
                    new RuleBuilder().Variation(1).Clauses(
                        ClauseBuilder.ShouldMatchSegment(_bigSegment.Key)
                    ).Build()
                )
                .Build();
            _testData.UsePreconfiguredFlag(_flag);
            _testData.UsePreconfiguredSegment(_bigSegment);

            _storeMock = new MockBigSegmentStore();
            _storeFactory = _storeMock.AsSingletonFactory<IBigSegmentStore>();
            _storeMock.SetupMetadataReturns(new StoreMetadata { LastUpToDate = UnixMillisecondTime.Now });
        }

        private LdClient MakeClient()
        {
            var config = BasicConfig()
                .BigSegments(Components.BigSegments(_storeFactory))
                .DataSource(_testData)
                .Build();
            return new LdClient(config);
        }

        [Fact]
        public void UserNotFound()
        {
            using (var client = MakeClient())
            {
                var result = client.BoolVariationDetail(_flag.Key, _user, false);
                Assert.False(result.Value);
                Assert.Equal(BigSegmentsStatus.Healthy, result.Reason.BigSegmentsStatus);
            }
        }

        [Fact]
        public void UserFound()
        {
            var membership = NewMembershipFromSegmentRefs(
                new string[] { MakeBigSegmentRef(_bigSegment) }, null);
            _storeMock.SetupMembershipReturns(BigSegmentContextKeyHash(_user.Key), membership);

            using (var client = MakeClient())
            {
                var result = client.BoolVariationDetail(_flag.Key, _user, false);
                Assert.True(result.Value);
                Assert.Equal(BigSegmentsStatus.Healthy, result.Reason.BigSegmentsStatus);
            }
        }

        [Fact]
        public void StoreError()
        {
            _storeMock.SetupMembershipThrows(BigSegmentContextKeyHash(_user.Key),
                new Exception("sorry"));

            using (var client = MakeClient())
            {
                var result = client.BoolVariationDetail(_flag.Key, _user, false);
                Assert.False(result.Value);
                Assert.Equal(BigSegmentsStatus.StoreError, result.Reason.BigSegmentsStatus);
            }
        }

        [Fact]
        public void StoreNotConfigured()
        {
            var config = BasicConfig()
                .DataSource(_testData)
                .Build();
            using (var client = new LdClient(config))
            {
                var result = client.BoolVariationDetail(_flag.Key, _user, false);
                Assert.False(result.Value);
                Assert.Equal(BigSegmentsStatus.NotConfigured, result.Reason.BigSegmentsStatus);
            }
        }
    }
}
