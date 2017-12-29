using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace LaunchDarkly.Client
{
    public class InMemoryFeatureStore : IFeatureStore
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<InMemoryFeatureStore>();
        private static readonly int RwLockMaxWaitMillis = 1000;
        private readonly ReaderWriterLockSlim RwLock = new ReaderWriterLockSlim();
        private readonly IDictionary<string, FeatureFlag> Features = new Dictionary<string, FeatureFlag>();
        private bool _initialized = false;

        FeatureFlag IFeatureStore.Get(string key)
        {
            try
            {
                RwLock.TryEnterReadLock(RwLockMaxWaitMillis);
                FeatureFlag f;
                if (!Features.TryGetValue(key, out f))
                {
                    Logger.LogWarning("Attempted to get feature with key: {0} not found in feature store. Returning null.",
                        key);
                    return null;
                }
                if (f.Deleted)
                {
                    Logger.LogWarning("Attempted to get deleted feature with key:{0} from feature store. Returning null.",
                        key);
                    return null;
                }
                return f;
            }
            finally
            {
                RwLock.ExitReadLock();
            }
        }

        IDictionary<string, FeatureFlag> IFeatureStore.All()
        {
            try
            {
                RwLock.TryEnterReadLock(RwLockMaxWaitMillis);
                IDictionary<string, FeatureFlag> fs = new Dictionary<string, FeatureFlag>();
                foreach (var feature in Features)
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
                RwLock.ExitReadLock();
            }
        }

        void IFeatureStore.Init(IDictionary<string, FeatureFlag> features)
        {
            try
            {
                RwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
                Features.Clear();
                foreach (var feature in features)
                {
                    Features[feature.Key] = feature.Value;
                }
                _initialized = true;
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        void IFeatureStore.Delete(string key, int version)
        {
            try
            {
                RwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
                FeatureFlag f;
                if (Features.TryGetValue(key, out f) && f.Version < version)
                {
                    f.Deleted = true;
                    f.Version = version;
                    Features[key] = f;
                }
                else if (f == null)
                {
                    f = new FeatureFlag();
                    f.Deleted = true;
                    f.Version = version;
                    Features[key] = f;
                }
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        void IFeatureStore.Upsert(string key, FeatureFlag featureFlag)
        {
            try
            {
                RwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
                FeatureFlag old;
                if (!Features.TryGetValue(key, out old) || old.Version < featureFlag.Version)
                {
                    Features[key] = featureFlag;
                }
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        bool IFeatureStore.Initialized()
        {
            return _initialized;
        }
    }
}