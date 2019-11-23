using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaunchDarkly.Client.Interfaces;

namespace LaunchDarkly.Client.Utils.Tests
{
    // Runs all the CachingStoreWrapper tests with an async data store implementation.
    public class CachingStoreWrapperTestAsync : CachingStoreWrapperTestBase<MockCoreAsync>
    {
        public CachingStoreWrapperTestAsync() : base(new MockCoreAsync()) { }

        protected override CachingStoreWrapperBuilder MakeWrapperBase()
        {
            return CachingStoreWrapper.Builder(_core);
        }
    }
    
    public class MockCoreAsync : MockCoreBase, IDataStoreCoreAsync
    {
        // This just ensures that we're really running an asynchronous task, even though we
        // aren't doing any I/O. If we just wrapped everything in Task.CompletedTask(), the
        // task scheduler wouldn't come into play.
        private async Task ArbitraryTask()
        {
            await Task.Delay(TimeSpan.FromTicks(1));
        }

        public async Task<IVersionedData> GetInternalAsync(IVersionedDataKind kind, string key)
        {
            await ArbitraryTask();
            return GetInternal(kind, key);
        }

        public async Task<IDictionary<string, IVersionedData>> GetAllInternalAsync(IVersionedDataKind kind)
        {
            await ArbitraryTask();
            return GetAllInternal(kind);
        }

        public async Task InitInternalAsync(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData)
        {
            await ArbitraryTask();
            InitInternal(allData);
        }

        public async Task<IVersionedData> UpsertInternalAsync(IVersionedDataKind kind, IVersionedData item)
        {
            await ArbitraryTask();
            return UpsertInternal(kind, item);
        }

        public async Task<bool> InitializedInternalAsync()
        {
            await ArbitraryTask();
            return InitializedInternal();
        }
    }
}
