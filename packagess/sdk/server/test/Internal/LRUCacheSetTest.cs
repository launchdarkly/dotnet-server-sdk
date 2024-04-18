using Xunit;

namespace LaunchDarkly.Sdk.Server.Internal
{
    public class LRUCacheTest
    {
        [Fact]
        public void AddReturnsFalseForNeverSeenValue()
        {
            LRUCacheSet<string> lru = new LRUCacheSet<string>(10);
            Assert.False(lru.Add("a"));
        }

        [Fact]
        public void AddReturnsTrueForPreviouslySeenValue()
        {
            LRUCacheSet<string> lru = new LRUCacheSet<string>(10);
            lru.Add("a");
            Assert.True(lru.Add("a"));
        }

        [Fact]
        public void OldestValueIsForgottenIfCapacityExceeded()
        {
            LRUCacheSet<string> lru = new LRUCacheSet<string>(2);
            lru.Add("a");
            lru.Add("b");
            lru.Add("c");
            Assert.True(lru.Add("c"));
            Assert.True(lru.Add("b"));
            Assert.False(lru.Add("a"));
        }

        [Fact]
        public void ValueBecomesNewEachTimeItIsAdded()
        {
            LRUCacheSet<string> lru = new LRUCacheSet<string>(2);
            lru.Add("a");
            lru.Add("b");
            lru.Add("a");
            lru.Add("c");
            Assert.True(lru.Add("c"));
            Assert.True(lru.Add("a"));
            Assert.False(lru.Add("b"));
        }
    }
}
