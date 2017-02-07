using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LaunchDarkly.Client
{
    internal class PollingProcessor : IUpdateProcessor
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<PollingProcessor>();
        private readonly Configuration _config;
        private readonly FeatureRequestor _featureRequestor;
        private readonly IFeatureStore _featureStore;
        private readonly CancellationTokenSource _stopCts = new CancellationTokenSource();



        internal PollingProcessor(Configuration config, FeatureRequestor featureRequestor, IFeatureStore featureStore)
        {
            _config = config;
            _featureRequestor = featureRequestor;
            _featureStore = featureStore;
        }
        

        void IUpdateProcessor.Start()
        {
            Logger.LogInformation("Starting LaunchDarkly PollingProcessor with interval: " +
                                  (int) _config.PollingInterval.TotalMilliseconds + " milliseconds");
            var stopToken = _stopCts.Token;
            Task.Run(() => UpdateTaskLoopAsync(stopToken));
        }

        private async Task UpdateTaskLoopAsync(CancellationToken stopToken)
        {
            while (!stopToken.IsCancellationRequested)
            {
                await UpdateTaskAsync();
                try
                {
                    await Task.Delay(_config.PollingInterval, stopToken);
                }
                catch (OperationCanceledException) { }
            }
        }

        private async Task UpdateTaskAsync()
        {
            try
            {
                var allFeatures = await _featureRequestor.MakeAllRequestAsync();
                if (allFeatures != null)
                {
                    _featureStore.Init(allFeatures);
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
            _stopCts.Cancel();
        }
    }
}