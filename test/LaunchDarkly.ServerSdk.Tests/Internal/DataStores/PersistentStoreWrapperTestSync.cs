using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    // Runs all the PersistentStoreWrapper tests with a synchronous data store implementation.
    public class PersistentStoreWrapperTestSync : PersistentStoreWrapperTestBase<MockCoreSync>
    {
        public PersistentStoreWrapperTestSync(ITestOutputHelper testOutput) :
            base(new MockCoreSync(), testOutput) { }

        internal override PersistentStoreWrapper MakeWrapper(TestParams testParams)
        {
            _core.PersistOnlyAsString = testParams.PersistMode.PersistOnlyAsString;
            return new PersistentStoreWrapper(_core, testParams.CacheMode.CacheConfig, _dataStoreUpdates, BasicTaskExecutor, TestLogger);
        }
    }
    
    public class MockCoreSync : MockCoreBase, IPersistentDataStore
    {
        // The IPersistentDataStore methods are already implemented in the base class,
        // we're just adding the interface to mark this as the sync implementation
    }
}
