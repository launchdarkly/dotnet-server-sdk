using System;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal.Concurrent;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal sealed class DataSourceStatusProviderImpl : IDataSourceStatusProvider
    {
        private readonly DataSourceUpdatesImpl _dataSourceUpdates;
        
        internal DataSourceStatusProviderImpl(DataSourceUpdatesImpl dataSourceUpdates)
        {
            _dataSourceUpdates = dataSourceUpdates;
        }

        public DataSourceStatus Status => _dataSourceUpdates.LastStatus;

        public event EventHandler<DataSourceStatus> StatusChanged
        {
            add
            {
                _dataSourceUpdates.StatusChanged += value;
            }
            remove
            {
                _dataSourceUpdates.StatusChanged -= value;
            }
        }

        public bool WaitFor(DataSourceState desiredState, TimeSpan timeout) =>
            AsyncUtils.WaitSafely(() => _dataSourceUpdates.WaitForAsync(desiredState, timeout));

        public Task<bool> WaitForAsync(DataSourceState desiredState, TimeSpan timeout) =>
            _dataSourceUpdates.WaitForAsync(desiredState, timeout);
    }
}
