using System;
using System.Threading;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class DataSourceStatusProviderImplTest : BaseTest
    {
        private readonly DataSourceUpdatesImpl updates;
        private readonly DataSourceStatusProviderImpl statusProvider;

        public DataSourceStatusProviderImplTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            var store = new InMemoryDataStore();
            updates = new DataSourceUpdatesImpl(
                store,
                new DataStoreStatusProviderImpl(store, new DataStoreUpdatesImpl(BasicTaskExecutor, TestLogger)),
                BasicTaskExecutor,
                TestLogger,
                null
                );
            statusProvider = new DataSourceStatusProviderImpl(updates);
        }

        [Fact]
        public void Status()
        {
            Assert.Equal(DataSourceState.Initializing, statusProvider.Status.State);

            var timeBefore = DateTime.Now;
            var errorInfo = DataSourceStatus.ErrorInfo.FromHttpError(500);

            updates.UpdateStatus(DataSourceState.Valid, errorInfo);

            var newStatus = statusProvider.Status;
            Assert.Equal(DataSourceState.Valid, newStatus.State);
            Assert.InRange(newStatus.StateSince, errorInfo.Time, errorInfo.Time.AddSeconds(1));
            Assert.Equal(errorInfo, newStatus.LastError);
        }

        [Fact]
        public void StatusListeners()
        {
            var statuses = new EventSink<DataSourceStatus>();
            statusProvider.StatusChanged += statuses.Add;

            var unwantedStatuses = new EventSink<DataSourceStatus>();
            EventHandler<DataSourceStatus> listener2 = unwantedStatuses.Add;
            statusProvider.StatusChanged += listener2;
            statusProvider.StatusChanged -= listener2; // testing that a listener can be unregistered

            updates.UpdateStatus(DataSourceState.Valid, null);

            var newStatus = statuses.ExpectValue();
            Assert.Equal(DataSourceState.Valid, newStatus.State);

            statuses.ExpectNoValue();
        }

        [Fact]
        public void WaitForStatusWithStatusAlreadyCorrect()
        {
            updates.UpdateStatus(DataSourceState.Valid, null);

            var success = statusProvider.WaitFor(DataSourceState.Valid, TimeSpan.FromMilliseconds(50));
            Assert.True(success);
        }

        [Fact]
        public async void WaitForStatusWithStatusAlreadyCorrectAsync()
        {
            updates.UpdateStatus(DataSourceState.Valid, null);

            var success = await statusProvider.WaitForAsync(DataSourceState.Valid, TimeSpan.FromMilliseconds(50));
            Assert.True(success);
        }

        [Fact]
        public void WaitForStatusSucceeds()
        {
            new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                updates.UpdateStatus(DataSourceState.Valid, null);
            }).Start();

            var success = statusProvider.WaitFor(DataSourceState.Valid, TimeSpan.Zero);
            Assert.True(success);
        }

        [Fact]
        public async void WaitForStatusSucceedsAsync()
        {
            new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                updates.UpdateStatus(DataSourceState.Valid, null);
            }).Start();

            var success = await statusProvider.WaitForAsync(DataSourceState.Valid, TimeSpan.Zero);
            Assert.True(success);
        }

        [Fact]
        public void WaitForStatusTimesOut()
        {
            var timeStart = DateTime.Now;
            var success = statusProvider.WaitFor(DataSourceState.Valid, TimeSpan.FromMilliseconds(200));
            var timeEnd = DateTime.Now;
            Assert.False(success);
            Assert.InRange(timeEnd.Subtract(timeStart).TotalMilliseconds, 100, 30000);
            // We have to use a very broad range assertion because the timing is highly dependent on how
            // many tasks are running, as well as platform differences. We just want to make sure it didn't
            // return immediately.
        }

        [Fact]
        public async void WaitForStatusTimesOutAsync()
        {
            var timeStart = DateTime.Now;
            var success = await statusProvider.WaitForAsync(DataSourceState.Valid, TimeSpan.FromMilliseconds(200));
            var timeEnd = DateTime.Now;
            Assert.False(success);
            Assert.InRange(timeEnd.Subtract(timeStart).TotalMilliseconds, 100, 30000);
        }

        [Fact]
        public void WaitForStatusEndsIfShutDown()
        {
            new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                updates.UpdateStatus(DataSourceState.Off, null);
            }).Start();

            var success = statusProvider.WaitFor(DataSourceState.Valid, TimeSpan.Zero);
            Assert.False(success);
        }

        [Fact]
        public async void WaitForStatusEndsIfShutDownAsync()
        {
            new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                updates.UpdateStatus(DataSourceState.Off, null);
            }).Start();

            var success = await statusProvider.WaitForAsync(DataSourceState.Valid, TimeSpan.Zero);
            Assert.False(success);
        }
    }
}
