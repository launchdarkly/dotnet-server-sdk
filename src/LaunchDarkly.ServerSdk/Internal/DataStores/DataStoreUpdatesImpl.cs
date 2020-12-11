using System;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    internal class DataStoreUpdatesImpl : IDataStoreUpdates
    {
        private readonly TaskExecutor _taskExecutor;
        private readonly object _stateLock = new object();

        private DataStoreStatus _currentStatus = new DataStoreStatus
        {
            Available = true,
            RefreshNeeded = false
        };

        internal DataStoreStatus Status
        {
            get
            {
                lock(_stateLock)
                {
                    return _currentStatus;
                }
            }
        }

        internal event EventHandler<DataStoreStatus> StatusChanged;

        internal DataStoreUpdatesImpl(TaskExecutor taskExecutor)
        {
            _taskExecutor = taskExecutor;
        }

        public void UpdateStatus(DataStoreStatus newStatus)
        {
            lock (_stateLock)
            {
                if (newStatus.Equals(_currentStatus))
                {
                    return;
                }
                _currentStatus = newStatus;
            }
            _taskExecutor.ScheduleEvent(this, newStatus, StatusChanged);
        }
    }
}
