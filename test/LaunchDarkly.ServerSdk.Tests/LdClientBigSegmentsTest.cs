using System;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.BigSegments.BigSegmentsInternalTypes;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientBigSegmentsTest : BaseTest
    {
        private TestData _testData;
        private FeatureFlag _flag;
        private Segment _bigSegment;
        private User _user;
        private Mock<IBigSegmentStore> _storeMock;
        private IBigSegmentStoreFactory _storeFactory;

        public LdClientBigSegmentsTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            _testData = TestData.DataSource();

            _user = User.WithKey("userkey");
            _bigSegment = new SegmentBuilder("segmentkey")
                .Unbounded(true)
                .Generation(1)
                .Build();
            _flag = new FeatureFlagBuilder("flagkey")
                .On(true)
                .Variations(LdValue.Of(false), LdValue.Of(true))
                .FallthroughVariation(0)
                .Rules(
                    new RuleBuilder().Variation(1).Clauses(
                        ClauseBuilder.ShouldMatchSegment(_bigSegment.Key)
                    ).Build()
                )
                .Build();
            _testData.UsePreconfiguredFlag(_flag);
            _testData.UsePreconfiguredSegment(_bigSegment);

            _storeMock = new Mock<IBigSegmentStore>();
            var store = _storeMock.Object;
            var storeFactoryMock = new Mock<IBigSegmentStoreFactory>();
            _storeFactory = storeFactoryMock.Object;
            storeFactoryMock.Setup(f => f.CreateBigSegmentStore(It.IsAny<LdClientContext>())).Returns(store);

            _storeMock.Setup(s => s.GetMetadataAsync()).ReturnsAsync(
                new StoreMetadata { LastUpToDate = UnixMillisecondTime.Now });
        }

        private LdClient MakeClient()
        {
            var config = Configuration.Builder("")
                .BigSegments(Components.BigSegments(_storeFactory))
                .DataSource(_testData)
                .Events(Components.NoEvents)
                .Logging(testLogging)
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
            _storeMock.Setup(s => s.GetMembershipAsync(BigSegmentUserKeyHash(_user.Key)))
                .ReturnsAsync(membership);

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
            _storeMock.Setup(s => s.GetMembershipAsync(BigSegmentUserKeyHash(_user.Key)))
                .ThrowsAsync(new Exception("sorry"));

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
            var config = Configuration.Builder("")
                .DataSource(_testData)
                .Events(Components.NoEvents)
                .Logging(testLogging)
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
