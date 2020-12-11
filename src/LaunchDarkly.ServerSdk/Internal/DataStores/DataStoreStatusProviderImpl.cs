using System;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    internal sealed class DataStoreStatusProviderImpl : IDataStoreStatusProvider
    {
        private readonly IDataStore _dataStore;
        private readonly DataStoreUpdatesImpl _dataStoreUpdates;

        public DataStoreStatus Status => _dataStoreUpdates.Status;

        public bool StatusMonitoringEnabled => _dataStore.StatusMonitoringEnabled;

        public event EventHandler<DataStoreStatus> StatusChanged
        {
            add
            {
                _dataStoreUpdates.StatusChanged += value;
            }
            remove
            {
                _dataStoreUpdates.StatusChanged -= value;
            }
        }

        internal DataStoreStatusProviderImpl(
            IDataStore dataStore,
            DataStoreUpdatesImpl dataStoreUpdates
            )
        {
            _dataStore = dataStore;
            _dataStoreUpdates = dataStoreUpdates;
        }
    }
}
