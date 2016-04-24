using System.Collections.Generic;
using System.Threading;

namespace LaunchDarkly.Client
{
    public class InMemoryFeatureStore : IFeatureStore
    {
        private static readonly int RwLockMaxWaitMillis = 1000;
        private readonly ReaderWriterLock RwLock = new ReaderWriterLock();
        private readonly IDictionary<string, Feature> Features = new Dictionary<string, Feature>();
        private bool _initialized = false;

        Feature IFeatureStore.Get(string key)
        {
            try
            {
                RwLock.AcquireReaderLock(RwLockMaxWaitMillis);
                Feature f;
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

        IDictionary<string, Feature> IFeatureStore.All()
        {
            try
            {
                RwLock.AcquireReaderLock(RwLockMaxWaitMillis);
                IDictionary<string, Feature> fs = new Dictionary<string, Feature>();
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

        void IFeatureStore.Init(IDictionary<string, Feature> features)
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
                Feature f;
                if (Features.TryGetValue(key, out f) && f.Version < version)
                {
                    f.Deleted = true;
                    f.Version = version;
                    Features[key] = f;
                }
                else if (f == null)
                {
                    f = new Feature();
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

        void IFeatureStore.Upsert(string key, Feature feature)
        {
            try
            {
                RwLock.AcquireWriterLock(RwLockMaxWaitMillis);
                Feature old;
                if (!Features.TryGetValue(key, out old) || old.Version < feature.Version)
                {
                    Features[key] = feature;
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
