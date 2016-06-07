using System.Collections.Generic;
using System.Threading;

namespace LaunchDarkly.Client
{
    public class InMemoryFeatureStore : IFeatureStore
    {
        private static readonly int RwLockMaxWaitMillis = 1000;
        private readonly ReaderWriterLock RwLock = new ReaderWriterLock();
        private readonly IDictionary<string, FeatureFlag> Features = new Dictionary<string, FeatureFlag>();
        private bool _initialized = false;

        FeatureFlag IFeatureStore.Get(string key)
        {
            try
            {
                RwLock.AcquireReaderLock(RwLockMaxWaitMillis);
                FeatureFlag f;
                if (!Features.TryGetValue(key, out f) || f.Deleted)
                {
                    return null;
                }
                return f;
            }
            finally
            {
                RwLock.ReleaseReaderLock();
            }
        }

        IDictionary<string, FeatureFlag> IFeatureStore.All()
        {
            try
            {
                RwLock.AcquireReaderLock(RwLockMaxWaitMillis);
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
                RwLock.ReleaseReaderLock();
            }
        }

        void IFeatureStore.Init(IDictionary<string, FeatureFlag> features)
        {
            try
            {
                RwLock.AcquireWriterLock(RwLockMaxWaitMillis);
                Features.Clear();
                foreach (var feature in features)
                {
                    Features[feature.Key] = feature.Value;
                }
                _initialized = true;
            }
            finally
            {
                RwLock.ReleaseWriterLock();
            }
        }

        void IFeatureStore.Delete(string key, int version)
        {
            try
            {
                RwLock.AcquireWriterLock(RwLockMaxWaitMillis);
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
                RwLock.ReleaseWriterLock();
            }
        }

        void IFeatureStore.Upsert(string key, FeatureFlag featureFlag)
        {
            try
            {
                RwLock.AcquireWriterLock(RwLockMaxWaitMillis);
                FeatureFlag old;
                if (!Features.TryGetValue(key, out old) || old.Version < featureFlag.Version)
                {
                    Features[key] = featureFlag;
                }
            }
            finally
            {
                RwLock.ReleaseWriterLock();
            }
        }

        bool IFeatureStore.Initialized()
        {
            return _initialized;
        }
    }
}
