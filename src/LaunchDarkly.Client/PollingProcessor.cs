using LaunchDarkly.Client.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    class PollingProcessor : IUpdateProcessor
    {
        private static ILog Logger = LogProvider.For<PollingProcessor>();
        private static int UNINITIALIZED = 0;
        private static int INITIALIZED = 1;
        private Configuration _config;
        private FeatureRequestor _featureRequestor;
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
            Logger.Info("Starting LaunchDarkly PollingProcessor with interval: " + (int)_config.PollingInterval.TotalMilliseconds + " milliseconds");
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
                var allFeatures = await _featureRequestor.MakeAllRequestAsync(true);
                Logger.Debug("Retrieved " + allFeatures.Count + " features");
                _featureStore.Init(allFeatures);

                //We can't use bool in CompareExchange because it is not a reference type.
                if (Interlocked.CompareExchange(ref _initialized, INITIALIZED, UNINITIALIZED) == 0)
                {
                    _initTask.SetResult(true);
                    Logger.Info("Initialized LaunchDarkly Polling Processor.");
                }
            }
            catch ( AggregateException ex ) {
                Logger.Error( string.Format( "Error Updating features: '{0}'", ex.Flatten().Message ) );
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("Error Updating features: '{0}'", ex.Message));
            }
        }

        void IDisposable.Dispose()
        {
            Logger.Info("Stopping LaunchDarkly PollingProcessor");
            _disposed = true;
        }
    }
}
