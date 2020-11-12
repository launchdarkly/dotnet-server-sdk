using System;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Helpers;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal sealed class PollingProcessor : IDataSource
    {
        private static int UNINITIALIZED = 0;
        private static int INITIALIZED = 1;
        private readonly Configuration _config;
        private readonly IFeatureRequestor _featureRequestor;
        private readonly IDataStoreUpdates _dataStoreUpdates;
        private int _initialized = UNINITIALIZED;
        private readonly TaskCompletionSource<bool> _initTask;
        private readonly Logger _log;
        private volatile bool _disposed;


        internal PollingProcessor(LdClientContext context,
            IFeatureRequestor featureRequestor, IDataStoreUpdates dataStoreUpdates)
        {
            _config = context.Configuration;
            _featureRequestor = featureRequestor;
            _dataStoreUpdates = dataStoreUpdates;
            _initTask = new TaskCompletionSource<bool>();
            _log = context.Logger.SubLogger(LogNames.DataSourceSubLog);
        }

        bool IDataSource.Initialized()
        {
            return _initialized == INITIALIZED;
        }

        Task<bool> IDataSource.Start()
        {
            _log.Info("Starting LaunchDarkly PollingProcessor with interval: {0} milliseconds",
                _config.PollingInterval.TotalMilliseconds);

            Task.Run(() => UpdateTaskLoopAsync());
            return _initTask.Task;
        }

        private async Task UpdateTaskLoopAsync()
        {
            while (!_disposed)
            {
                await UpdateTaskAsync();
                await Task.Delay(_config.PollingInterval);
            }
        }

        private async Task UpdateTaskAsync()
        {
            try
            {
                var allData = await _featureRequestor.GetAllDataAsync();
                if (allData != null)
                {
                    _dataStoreUpdates.Init(allData.ToInitData());

                    //We can't use bool in CompareExchange because it is not a reference type.
                    if (Interlocked.CompareExchange(ref _initialized, INITIALIZED, UNINITIALIZED) == 0)
                    {
                        _initTask.SetResult(true);
                        _log.Info("Initialized LaunchDarkly Polling Processor.");
                    }
                }
            }
            catch (AggregateException ex)
            {
                _log.Error("Error Updating features: {0}",
                    LogValues.ExceptionSummary(ex.Flatten()));
                _log.Debug(LogValues.ExceptionTrace(ex));
            }
            catch (UnsuccessfulResponseException ex)
            {
                _log.Error(Util.HttpErrorMessage(ex.StatusCode, "polling request", "will retry"));
                if (!Util.IsHttpErrorRecoverable(ex.StatusCode))
                {
                    try
                    {
                        // if client is initializing, make it stop waiting
                        _initTask.SetResult(true);
                    }
                    catch (InvalidOperationException)
                    {
                        // the task was already set - nothing more to do
                    }
                    ((IDisposable)this).Dispose();
                }
            }
            catch (Exception ex)
            {
                _log.Error("Error Updating features: {0}", LogValues.ExceptionSummary(ex));
                _log.Debug(LogValues.ExceptionTrace(ex));
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _log.Info("Stopping LaunchDarkly PollingProcessor");
                _disposed = true;
                _featureRequestor.Dispose();
            }
        }
    }
}