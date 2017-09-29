using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using LaunchDarklyClient.Interfaces;

namespace LaunchDarklyClient
{
	internal class PollingProcessor : IUpdateProcessor
	{
		private static readonly ILog log = LogManager.GetLogger<PollingProcessor>();

		private const int Uninitialized = 0;
		private const int Initialized = 1;
		private readonly Configuration config;
		private readonly FeatureRequestor featureRequestor;
		private readonly IFeatureStore featureStore;
		private readonly TaskCompletionSource<bool> initTask;
		private bool disposed;
		private int isInitialized = Uninitialized;

		internal PollingProcessor(Configuration config, FeatureRequestor featureRequestor, IFeatureStore featureStore)
		{
			try
			{
				log.Trace($"Start constructor {nameof(PollingProcessor)}(Configuration, FeatureRequestor, IFeatureStore)");

				this.config = config;
				this.featureRequestor = featureRequestor;
				this.featureStore = featureStore;
				initTask = new TaskCompletionSource<bool>();
			}
			finally
			{
				log.Trace($"End constructor {nameof(PollingProcessor)}(Configuration, FeatureRequestor, IFeatureStore)");
			}
		}

		bool IUpdateProcessor.Initialized()
		{
			try
			{
				log.Trace($"Start {nameof(IUpdateProcessor.Initialized)}");

				return isInitialized == Initialized;
			}
			finally
			{
				log.Trace($"End {nameof(IUpdateProcessor.Initialized)}");
			}
		}

		TaskCompletionSource<bool> IUpdateProcessor.Start()
		{
			try
			{
				log.Trace($"Start {nameof(IUpdateProcessor.Start)}");

				log.Info($"Starting LaunchDarkly PollingProcessor with interval: {(int) config.PollingInterval.TotalMilliseconds} milliseconds");
				Task.Run(UpdateTaskLoopAsync);
				return initTask;
			}
			finally
			{
				log.Trace($"End {nameof(IUpdateProcessor.Start)}");
			}
		}

		void IDisposable.Dispose()
		{
			try
			{
				log.Trace($"Start {nameof(IDisposable.Dispose)}");

				log.Info("Stopping LaunchDarkly PollingProcessor");
				disposed = true;
			}
			finally
			{
				log.Trace($"End {nameof(IDisposable.Dispose)}");
			}
		}

		private async Task UpdateTaskLoopAsync()
		{
			try
			{
				log.Trace($"Start {nameof(UpdateTaskLoopAsync)}");

				while (!disposed)
				{
					await UpdateTaskAsync();
					await Task.Delay(config.PollingInterval);
				}
			}
			finally
			{
				log.Trace($"End {nameof(UpdateTaskLoopAsync)}");
			}
		}

		private async Task UpdateTaskAsync()
		{
			try
			{
				log.Trace($"Start {nameof(UpdateTaskAsync)}");

				try
				{
					IDictionary<string, FeatureFlag> allFeatures = await featureRequestor.MakeAllRequestAsync();
					if (allFeatures != null)
					{
						featureStore.Init(allFeatures);

						//We can't use bool in CompareExchange because it is not a reference type.
						if (Interlocked.CompareExchange(ref isInitialized, Initialized, Uninitialized) == 0)
						{
							initTask.SetResult(true);
							log.Info("Initialized LaunchDarkly Polling Processor.");
						}
					}
				}
				catch (AggregateException ex)
				{
					log.Error($"Error Updating features: '{Util.ExceptionMessage(ex.Flatten())}'");
				}
				catch (Exception ex)
				{
					log.Error($"Error Updating features: '{Util.ExceptionMessage(ex)}'");
				}
			}
			finally
			{
				log.Trace($"End {nameof(UpdateTaskAsync)}");
			}
		}
	}
}