using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Concurrent;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    internal sealed class DataStoreUpdatesImpl : IDataStoreUpdates
    {
        private readonly TaskExecutor _taskExecutor;

        private StateMonitor<DataStoreStatus, DataStoreStatus> _status;

        internal DataStoreStatus Status => _status.Current;

        internal event EventHandler<DataStoreStatus> StatusChanged;

        internal DataStoreUpdatesImpl(TaskExecutor taskExecutor, Logger log)
        {
            _taskExecutor = taskExecutor;
            var initialStatus = new DataStoreStatus
            {
                Available = true,
                RefreshNeeded = false
            };
            _status = new StateMonitor<DataStoreStatus, DataStoreStatus>(initialStatus, MaybeUpdate, log);
        }

        private DataStoreStatus? MaybeUpdate(DataStoreStatus lastValue, DataStoreStatus newValue) =>
            newValue.Equals(lastValue) ? (DataStoreStatus?)null : newValue;
            
        public void UpdateStatus(DataStoreStatus newStatus)
        {
            if (_status.Update(newStatus, out _))
            {
                _taskExecutor.ScheduleEvent(newStatus, StatusChanged);
            }
        }
    }
}
