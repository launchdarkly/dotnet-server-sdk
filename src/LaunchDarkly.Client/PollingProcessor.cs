using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LaunchDarkly.Client
{
    internal class PollingProcessor : IUpdateProcessor
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<PollingProcessor>();
        private static int UNINITIALIZED = 0;
        private static int INITIALIZED = 1;
        private readonly Configuration _config;
        private readonly FeatureRequestor _featureRequestor;
        private readonly IFeatureStore _featureStore;
        private int _initialized = UNINITIALIZED;
        private readonly TaskCompletionSource<bool> _initTask;
        private bool _disposed;


        internal PollingProcessor(Configuration config, FeatureRequestor featureRequestor, IFeatureStore featureStore)
        {
            _config = config;
            _featureRequestor = featureRequestor;
            _featureStore = featureStore;
            _initTask = new TaskCompletionSource<bool>();
        }

        bool IUpdateProcessor.Initialized()
        {
            return _initialized == INITIALIZED;
        }

        TaskCompletionSource<bool> IUpdateProcessor.Start()
        {
            Logger.LogInformation("Starting LaunchDarkly PollingProcessor with interval: " +
                                  (int) _config.PollingInterval.TotalMilliseconds + " milliseconds");
            Task.Run(() => UpdateTaskLoopAsync());
            return _initTask;
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
                var allFeatures = await _featureRequestor.GetAllFlagsAsync();
                if (allFeatures != null)
                {
                    _featureStore.Init(allFeatures);

                    //We can't use bool in CompareExchange because it is not a reference type.
                    if (Interlocked.CompareExchange(ref _initialized, INITIALIZED, UNINITIALIZED) == 0)
                    {
                        _initTask.SetResult(true);
                        Logger.LogInformation("Initialized LaunchDarkly Polling Processor.");
                    }
                }
            }
            catch (AggregateException ex)
            {
                Logger.LogError(string.Format("Error Updating features: '{0}'", Util.ExceptionMessage(ex.Flatten())));
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Error Updating features: '{0}'", Util.ExceptionMessage(ex)));
            }
        }


        void IDisposable.Dispose()
        {
            Logger.LogInformation("Stopping LaunchDarkly PollingProcessor");
            _disposed = true;
        }
    }
}