using System;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    public class DataStoreStatusProviderImplTest : BaseTest
    {
        private readonly MockDataStore _dataStore = new MockDataStore();
        private readonly DataStoreUpdatesImpl _dataStoreUpdates;
        private readonly DataStoreStatusProviderImpl _dataStoreStatusProvider;

        public DataStoreStatusProviderImplTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            _dataStoreUpdates = new DataStoreUpdatesImpl(BasicTaskExecutor, TestLogger);
            _dataStoreStatusProvider = new DataStoreStatusProviderImpl(_dataStore, _dataStoreUpdates);
        }

        [Fact]
        public void Status()
        {
            Assert.Equal(new DataStoreStatus { Available = true, RefreshNeeded = false },
                _dataStoreStatusProvider.Status);

            var status1 = new DataStoreStatus { Available = false, RefreshNeeded = false };
            _dataStoreUpdates.UpdateStatus(status1);

            Assert.Equal(status1, _dataStoreStatusProvider.Status);

            var status2 = new DataStoreStatus { Available = false, RefreshNeeded = true };
            _dataStoreUpdates.UpdateStatus(status2);

            Assert.Equal(status2, _dataStoreStatusProvider.Status);
        }

        [Fact]
        public void Listeners()
        {
            var statuses = new EventSink<DataStoreStatus>();
            _dataStoreStatusProvider.StatusChanged += statuses.Add;

            var unwantedStatuses = new EventSink<DataStoreStatus>();
            _dataStoreStatusProvider.StatusChanged += unwantedStatuses.Add;
            _dataStoreStatusProvider.StatusChanged -= unwantedStatuses.Add; // testing that a listener can be removed

            var status = new DataStoreStatus { Available = false, RefreshNeeded = false };
            _dataStoreUpdates.UpdateStatus(status);

            Assert.Equal(status, statuses.ExpectValue());
            unwantedStatuses.ExpectNoValue();
        }

        [Fact]
        public void StatusMonitoringEnabled()
        {
            Assert.False(_dataStoreStatusProvider.StatusMonitoringEnabled);

            _dataStore.StatusMonitoringEnabled = true;
            Assert.True(_dataStoreStatusProvider.StatusMonitoringEnabled);
        }
    }

    internal class MockDataStore : IDataStore
    {
        public bool StatusMonitoringEnabled { get; set; }

        public void Dispose() { }

        public DataStoreTypes.ItemDescriptor? Get(DataStoreTypes.DataKind kind, string key) =>
            throw new NotImplementedException();

        public DataStoreTypes.KeyedItems<DataStoreTypes.ItemDescriptor> GetAll(DataStoreTypes.DataKind kind) =>
            throw new NotImplementedException();

        public void Init(DataStoreTypes.FullDataSet<DataStoreTypes.ItemDescriptor> allData) =>
            throw new NotImplementedException();

        public bool Initialized() => throw new NotImplementedException();

        public bool Upsert(DataStoreTypes.DataKind kind, string key, DataStoreTypes.ItemDescriptor item) =>
            throw new NotImplementedException();
    }
}
