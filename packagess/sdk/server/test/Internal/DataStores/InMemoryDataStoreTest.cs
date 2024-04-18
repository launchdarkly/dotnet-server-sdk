
namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    public class InMemoryDataStoreTest : DataStoreTestBase
    {
        public InMemoryDataStoreTest()
        {
            store = new InMemoryDataStore();
        }
    }
}
