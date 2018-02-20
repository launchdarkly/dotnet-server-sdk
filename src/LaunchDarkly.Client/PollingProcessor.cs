using System;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace LaunchDarkly.Client
{
    internal class PollingProcessor : IUpdateProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PollingProcessor));
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

        Task<bool> IUpdateProcessor.Start()
        {
            Log.InfoFormat("Starting LaunchDarkly PollingProcessor with interval: {0} milliseconds",
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
                    _featureStore.Init(allData.ToGenericDictionary());

                    //We can't use bool in CompareExchange because it is not a reference type.
                    if (Interlocked.CompareExchange(ref _initialized, INITIALIZED, UNINITIALIZED) == 0)
                    {
                        _initTask.SetResult(true);
                        Log.Info("Initialized LaunchDarkly Polling Processor.");
                    }
                }
            }
            catch (AggregateException ex)
            {
                Log.ErrorFormat("Error Updating features: '{0}'",
                    ex,
                    Util.ExceptionMessage(ex.Flatten()));
            }
            catch (FeatureRequestorUnsuccessfulResponseException ex) when (ex.StatusCode == 401)
            {
                Log.ErrorFormat("Error Updating features: '{0}'", Util.ExceptionMessage(ex));
                Log.Error("Received 401 error, no further polling requests will be made since SDK key is invalid");
                ((IDisposable)this).Dispose();
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Error Updating features: '{0}'",
                    ex,
                    Util.ExceptionMessage(ex));
            }
        }


        void IDisposable.Dispose()
        {
            Log.Info("Stopping LaunchDarkly PollingProcessor");
            _disposed = true;
        }
    }
}