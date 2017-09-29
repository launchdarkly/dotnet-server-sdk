using System.Collections.Generic;
using System.Threading;
using Common.Logging;
using LaunchDarklyClient.Interfaces;

namespace LaunchDarklyClient
{
	public class InMemoryFeatureStore : IFeatureStore
	{
		private static readonly ILog log = LogManager.GetLogger<InMemoryFeatureStore>();

		private const int RwLockMaxWaitMillis = 1000;
		private readonly IDictionary<string, FeatureFlag> features = new Dictionary<string, FeatureFlag>();
		private readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();
		private bool initialized;

		FeatureFlag IFeatureStore.Get(string key)
		{
			try
			{
				log.Trace($"Start {nameof(IFeatureStore.Get)}");

				rwLock.TryEnterReadLock(RwLockMaxWaitMillis);
				FeatureFlag f;

				if (!features.TryGetValue(key, out f))
				{
					log.Warn($"Attempted to get feature with key: {key} not found in feature store. Returning null.");
					return null;
				}
				if (f.Deleted)
				{
					log.Warn($"Attempted to get deleted feature with key: {key} from feature store. Returning null.");
					return null;
				}

				return f;
			}
			finally
			{
				rwLock.ExitReadLock();
				log.Trace($"End {nameof(IFeatureStore.Get)}");
			}
		}

		IDictionary<string, FeatureFlag> IFeatureStore.All()
		{
			try
			{
				log.Trace($"Start {nameof(IFeatureStore.All)}");
				rwLock.TryEnterReadLock(RwLockMaxWaitMillis);
				IDictionary<string, FeatureFlag> fs = new Dictionary<string, FeatureFlag>();
				foreach (KeyValuePair<string, FeatureFlag> feature in features)
				{
					if (!feature.Value.Deleted)
					{
						fs[feature.Key] = feature.Value;
					}
				}
				return fs;
			}
			finally
			{
				rwLock.ExitReadLock();
				log.Trace($"End {nameof(IFeatureStore.All)}");
			}
		}

		void IFeatureStore.Init(IDictionary<string, FeatureFlag> updatedFeatures)
		{
			try
			{
				log.Trace($"Start {nameof(IFeatureStore.Init)}");
				rwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
				features.Clear();
				foreach (KeyValuePair<string, FeatureFlag> feature in updatedFeatures)
				{
					features[feature.Key] = feature.Value;
				}
				initialized = true;
			}
			finally
			{
				rwLock.ExitWriteLock();
				log.Trace($"End {nameof(IFeatureStore.Init)}");
			}
		}

		void IFeatureStore.Delete(string key, int version)
		{
			try
			{
				log.Trace($"Start {nameof(IFeatureStore.Delete)}");
				rwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
				FeatureFlag flag;
				if (features.TryGetValue(key, out flag) && flag.Version < version)
				{
					flag.Deleted = true;
					flag.Version = version;
					features[key] = flag;
				}
				else if (flag == null)
				{
					flag = new FeatureFlag
					{
						Deleted = true,
						Version = version
					};
					features[key] = flag;
				}
			}
			finally
			{
				rwLock.ExitWriteLock();
				log.Trace($"End {nameof(IFeatureStore.Delete)}");
			}
		}

		void IFeatureStore.Upsert(string key, FeatureFlag featureFlag)
		{
			try
			{
				log.Trace($"Start {nameof(IFeatureStore.Upsert)}");
				rwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
				FeatureFlag old;
				if (!features.TryGetValue(key, out old) || old.Version < featureFlag.Version)
				{
					features[key] = featureFlag;
				}
			}
			finally
			{
				rwLock.ExitWriteLock();
				log.Trace($"End {nameof(IFeatureStore.Upsert)}");
			}
		}

		bool IFeatureStore.Initialized()
		{
			try
			{
				log.Trace($"Start {nameof(IFeatureStore.Initialized)}");

				return initialized;
			}
			finally
			{
				log.Trace($"End {nameof(IFeatureStore.Initialized)}");
			}
		}
	}
}