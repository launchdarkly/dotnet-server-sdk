using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    // Runs all the CachingStoreWrapper tests with an async data store implementation.
    public class PersistentStoreWrapperTestAsync : PersistentStoreWrapperTestBase<MockCoreAsync>
    {
        public PersistentStoreWrapperTestAsync() : base(new MockCoreAsync()) { }

        internal override PersistentStoreWrapper MakeWrapperWithCacheConfig(DataStoreCacheConfig config)
        {
            return new PersistentStoreWrapper(_core, config);
        }
    }
    
    public class MockCoreAsync : MockCoreBase, IPersistentDataStoreAsync
    {
        // This just ensures that we're really running an asynchronous task, even though we
        // aren't doing any I/O. If we just wrapped everything in Task.CompletedTask(), the
        // task scheduler wouldn't come into play.
        private async Task ArbitraryTask()
        {
            await Task.Delay(TimeSpan.FromTicks(1));
        }

        public async Task<SerializedItemDescriptor?> GetAsync(DataKind kind, string key)
        {
            await ArbitraryTask();
            return Get(kind, key);
        }

        public async Task<IEnumerable<KeyValuePair<string, SerializedItemDescriptor>>> GetAllAsync(DataKind kind)
        {
            await ArbitraryTask();
            return GetAll(kind);
        }

        public async Task InitAsync(FullDataSet<SerializedItemDescriptor> allData)
        {
            await ArbitraryTask();
            Init(allData);
        }

        public async Task<bool> UpsertAsync(DataKind kind, string key, SerializedItemDescriptor item)
        {
            await ArbitraryTask();
            return Upsert(kind, key, item);
        }

        public async Task<bool> InitializedAsync()
        {
            await ArbitraryTask();
            return Initialized();
        }
    }
}
