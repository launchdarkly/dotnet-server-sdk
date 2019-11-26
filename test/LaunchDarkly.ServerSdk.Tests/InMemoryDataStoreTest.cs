
namespace LaunchDarkly.Sdk.Server
{
    public class InMemoryDataStoreTest : DataStoreTestBase
    {
        public InMemoryDataStoreTest()
        {
            store = new InMemoryDataStore();
        }
    }
}
