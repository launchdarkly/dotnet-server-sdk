using LaunchDarkly.Client;

namespace LaunchDarkly.Tests
{
    public class InMemoryDataStoreTest : DataStoreTestBase
    {
        public InMemoryDataStoreTest()
        {
            store = new InMemoryDataStore();
        }
    }
}
