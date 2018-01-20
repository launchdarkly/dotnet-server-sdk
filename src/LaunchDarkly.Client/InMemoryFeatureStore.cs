
namespace LaunchDarkly.Client
{
    public class InMemoryFeatureStore : InMemoryDataStore<FeatureFlag>, IFeatureStore
    {
        override protected FeatureFlag EmptyItem()
        {
            return new FeatureFlag();
        }

        override protected string ItemName()
        {
            return "feature";
        }
    }
}