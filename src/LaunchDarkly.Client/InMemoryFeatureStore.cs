using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    public class InMemoryFeatureStore : IFeatureStore
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<InMemoryFeatureStore>();
        private static readonly int RwLockMaxWaitMillis = 1000;
        private static int UNINITIALIZED = 0;
        private static int INITIALIZED = 1;
        private readonly ReaderWriterLockSlim RwLock = new ReaderWriterLockSlim();
        private readonly IDictionary<string, FeatureFlag> Features = new Dictionary<string, FeatureFlag>();
        private int _initialized = UNINITIALIZED;
        private readonly TaskCompletionSource<bool> _initTask = new TaskCompletionSource<bool>();
        private readonly BlockingCollection<string> _storeQueue = new BlockingCollection<string>();
        private string _versionIdentifier;

        protected virtual Task<string> LoadPersistedDataAsync()
        {
            return Task.FromResult<string>(null);
        }

        protected virtual Task StorePersistedDataAsync(string data)
        {
            return  Task.FromResult(0);
        }

        protected virtual bool IsPersisted => false;

        string IFeatureStore.VersionIdentifier
        {
            get
            {
                try
                {
                    RwLock.TryEnterReadLock(RwLockMaxWaitMillis);
                    return _versionIdentifier;
                }
                finally
                {
                    RwLock.ExitReadLock();
                }
            }
        }

        async Task IFeatureStore.LoadPersistedDataAsync()
        {
            if (!IsPersisted)
            {
                return;
            }
            try
            {
                var json = await this.LoadPersistedDataAsync();
                if (json == null)
                {
                    return;
                }
                var data = JsonConvert.DeserializeObject<FeatureRequestor.VersionedFeatureFlags>(json);
                if (data.FeatureFlags != null)
                {
                    // init persisted version to avoid write back
                    _versionIdentifier = data.VersionIdentifier;
                    ((IFeatureStore)this).Init(data.FeatureFlags, data.VersionIdentifier);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Unable to load persisted data: "+ex.Message);
            }
        }

        Task<bool> IFeatureStore.WaitForInitializationAsync()
        {
            return _initTask.Task;
        }
        FeatureFlag IFeatureStore.Get(string key)
        {
            try
            {
                RwLock.TryEnterReadLock(RwLockMaxWaitMillis);
                FeatureFlag f;
                if (!Features.TryGetValue(key, out f))
                {
                    Logger.LogWarning("Attempted to get feature with key: " + key +
                                      " not found in feature store. Returning null.");
                    return null;
                }
                if (f.Deleted)
                {
                    Logger.LogWarning("Attempted to get deleted feature with key: " + key +
                                      " from feature store. Returning null.");
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

        void IFeatureStore.Init(IDictionary<string, FeatureFlag> features, string versionIdentifier)
        {
            try
            {
                RwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
                Features.Clear();
                foreach (var feature in features)
                {
                    Features[feature.Key] = feature.Value;
                }
                if (_versionIdentifier != versionIdentifier)
                {
                    _versionIdentifier = versionIdentifier;
                    if (IsPersisted)
                    {
                        var json = JsonConvert.SerializeObject(new FeatureRequestor.VersionedFeatureFlags
                        {
                            FeatureFlags = features,
                            VersionIdentifier = versionIdentifier
                        });
                        _storeQueue.Add(json);
                    }
                }
                //We can't use bool in CompareExchange because it is not a reference type.
                if (Interlocked.CompareExchange(ref _initialized, INITIALIZED, UNINITIALIZED) == 0)
                {
                    _initTask.SetResult(true);
                    Logger.LogInformation("Initialized LaunchDarkly Feature store.");
                    if (IsPersisted)
                    {
                        StartStoreQueue();
                    }
                }
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        private void StartStoreQueue()
        {
            Task.Run(async () =>
            {
                // there is room for improvement there to make sure we don't block
                foreach (var storePayload in _storeQueue.GetConsumingEnumerable())
                {
                    await StorePersistedDataAsync(storePayload);
                }
            });
        }

        bool IFeatureStore.Initialized()
        {
            try
            {
                RwLock.TryEnterReadLock(RwLockMaxWaitMillis);
                return _initialized == INITIALIZED;
            }
            finally
            {
                RwLock.ExitReadLock();
            }
        }
    }
}