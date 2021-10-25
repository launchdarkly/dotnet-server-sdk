using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    public class DataStoreUpdatesImplTest : BaseTest
    {
        private readonly DataStoreUpdatesImpl updates;

        public DataStoreUpdatesImplTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            updates = new DataStoreUpdatesImpl(BasicTaskExecutor, TestLogger);
        }

        [Fact]
        public void UpdateStatusBroadcastsNewStatus()
        {
            var statuses = new EventSink<DataStoreStatus>();
            updates.StatusChanged += statuses.Add;

            var expectedStatus = new DataStoreStatus
            {
                Available = false,
                RefreshNeeded = true
            };
            updates.UpdateStatus(expectedStatus);

            var newStatus = statuses.ExpectValue();
            Assert.Equal(expectedStatus, newStatus);
            statuses.ExpectNoValue();
        }

        [Fact]
        public void UpdateStatusDoesNothingIfNewStatusIsSame()
        {
            var statuses = new EventSink<DataStoreStatus>();
            updates.StatusChanged += statuses.Add;

            updates.UpdateStatus(new DataStoreStatus
            {
                Available = true,
                RefreshNeeded = false
            });

            statuses.ExpectNoValue();
        }
    }
}
