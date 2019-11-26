using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Utils
{
    // Runs all the CachingStoreWrapper tests with a synchronous data store implementation.
    public class CachingStoreWrapperTestSync : CachingStoreWrapperTestBase<MockCoreSync>
    {
        public CachingStoreWrapperTestSync() : base(new MockCoreSync()) { }

        protected override CachingStoreWrapperBuilder MakeWrapperBase()
        {
            return CachingStoreWrapper.Builder(_core);
        }
    }
    
    public class MockCoreSync : MockCoreBase, IDataStoreCore
    {
        // The IDataStoreCore methods are already implemented in the base class,
        // we're just adding the interface to mark this as the sync implementation
    }
}
