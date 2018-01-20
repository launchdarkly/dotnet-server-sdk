
namespace LaunchDarkly.Client
{
    public class InMemorySegmentStore : InMemoryDataStore<Segment>, ISegmentStore
    {
        override protected Segment EmptyItem()
        {
            return new Segment();
        }

        override protected string ItemName()
        {
            return "segment";
        }
    }
}