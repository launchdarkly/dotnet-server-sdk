using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    // Runs all the PersistentStoreWrapper tests with a synchronous data store implementation.
    public class PersistentStoreWrapperTestSync : PersistentStoreWrapperTestBase<MockCoreSync>
    {
        public PersistentStoreWrapperTestSync() : base(new MockCoreSync()) { }

        internal override PersistentStoreWrapper MakeWrapperWithCacheConfig(DataStoreCacheConfig config)
        {
            return new PersistentStoreWrapper(_core, config);
        }
    }
    
    public class MockCoreSync : MockCoreBase, IPersistentDataStore
    {
        // The IPersistentDataStore methods are already implemented in the base class,
        // we're just adding the interface to mark this as the sync implementation
    }
}
