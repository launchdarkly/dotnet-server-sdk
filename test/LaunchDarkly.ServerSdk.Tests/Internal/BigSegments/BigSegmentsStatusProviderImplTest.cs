using System;
using LaunchDarkly.Sdk.Server.Interfaces;
using Moq;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.BigSegments
{
    public class BigSegmentsStatusProviderImplTest : BaseTest
    {
        public BigSegmentsStatusProviderImplTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void StatusProviderWithNoStoreWrapperHasNoOpProperties()
        {
            var sp = new BigSegmentStoreStatusProviderImpl(null);

            var status = sp.Status;
            Assert.False(status.Available);
            Assert.False(status.Stale);

            EventHandler<BigSegmentStoreStatus> handler = (sender, s) => { };
            sp.StatusChanged += handler;
            sp.StatusChanged -= handler;
        }

        [Fact]
        public void StatusProviderDelegatesToStoreWrapper()
        {
            var storeTimestamp = UnixMillisecondTime.Now.PlusMillis(-1000);
            var storeMetadata = new StoreMetadata { LastUpToDate = storeTimestamp };
            var storeMock = new Mock<IBigSegmentStore>();
            var store = storeMock.Object;
            var storeFactoryMock = new Mock<IBigSegmentStoreFactory>();
            var storeFactory = storeFactoryMock.Object;
            storeFactoryMock.Setup(f => f.CreateBigSegmentStore(BasicContext)).Returns(store);
            storeMock.Setup(s => s.GetMetadataAsync()).ReturnsAsync(storeMetadata);

            var bsConfig = Components.BigSegments(storeFactory)
                .StatusPollInterval(TimeSpan.FromMilliseconds(1))
                .StaleAfter(TimeSpan.FromDays(1));
            using (var sw = new BigSegmentStoreWrapper(
                bsConfig.CreateBigSegmentsConfiguration(BasicContext),
                BasicTaskExecutor,
                TestLogger
                ))
            {
                var sp = new BigSegmentStoreStatusProviderImpl(sw);
                var status = sp.Status;
                Assert.True(status.Available);
                Assert.False(status.Stale);
            }
        }
    }
}
