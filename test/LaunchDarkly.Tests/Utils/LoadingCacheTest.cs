using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LaunchDarkly.Client.Utils.Tests
{
    public class LoadingCacheTest
    {
        private TestValueGenerator valueGenerator = new TestValueGenerator();

        [Fact]
        public void GetNewlyComputedValue()
        {
            var cache = new LoadingCache<string, string>(valueGenerator.GetNextValue, null);
            Assert.Equal("key_value_1", cache.Get("key"));
        }

        [Fact]
        public void GetCachedValue()
        {
            var cache = new LoadingCache<string, string>(valueGenerator.GetNextValue, null);
            Assert.Equal("key_value_1", cache.Get("key"));
            Assert.Equal("key_value_1", cache.Get("key")); // value was not recomputed
            Assert.Equal(1, valueGenerator.TimesCalled);
        }

        [Fact]
        public void GetExplicitlySetValue()
        {
            var cache = new LoadingCache<string, string>(valueGenerator.GetNextValue, null);
            cache.Set("key", "other");
            Assert.Equal("other", cache.Get("key"));
        }

        [Fact]
        public void RemoveValue()
        {
            var cache = new LoadingCache<string, string>(valueGenerator.GetNextValue, null);
            Assert.Equal("foo_value_1", cache.Get("foo"));
            Assert.Equal("bar_value_2", cache.Get("bar"));
            cache.Remove("foo");
            Assert.Equal("foo_value_3", cache.Get("foo")); // value was recomputed
            Assert.Equal("bar_value_2", cache.Get("bar"));
        }

        [Fact]
        public void RemoveAllValues()
        {
            var cache = new LoadingCache<string, string>(valueGenerator.GetNextValue, null);
            Assert.Equal("foo_value_1", cache.Get("foo"));
            Assert.Equal("bar_value_2", cache.Get("bar"));
            cache.Clear();
            Assert.Equal("foo_value_3", cache.Get("foo")); // value was recomputed
            Assert.Equal("bar_value_4", cache.Get("bar")); // value was recomputed
        }

        [Fact]
        public void ComputedValueCanExpire()
        {
            var cache = new LoadingCache<string, string>(valueGenerator.GetNextValue,
                TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(25));
            Assert.Equal("key_value_1", cache.Get("key"));
            Thread.Sleep(TimeSpan.FromMilliseconds(150));
            Assert.Equal("key_value_2", cache.Get("key"));
        }

        [Fact]
        public void ComputedValueCanExpireEvenIfPurgeTaskHasNotRunYet()
        {
            var cache = new LoadingCache<string, string>(valueGenerator.GetNextValue,
                TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500));
            Assert.Equal("key_value_1", cache.Get("key"));
            Thread.Sleep(TimeSpan.FromMilliseconds(150));
            Assert.Equal("key_value_2", cache.Get("key"));
        }

        [Fact]
        public void ExplicitlySetValueCanExpire()
        {
            var cache = new LoadingCache<string, string>(valueGenerator.GetNextValue,
                TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(25));
            cache.Set("key", "other");
            Thread.Sleep(TimeSpan.FromMilliseconds(250));
            Assert.Equal("key_value_1", cache.Get("key"));
        }

        [Fact]
        public void MultipleRequestsForNewValueAreCoalesced()
        {
            var cache = new LoadingCache<string, string>(valueGenerator.GetNextValue, null);
            valueGenerator.Delay = TimeSpan.FromMilliseconds(200);
            var tasks = new Task[3];
            for (var i = 0; i < 3; i++)
            {
                tasks[i] = Task.Run(() => cache.Get("key"));
            }
            Task.WaitAll(tasks);
            Assert.Equal(1, valueGenerator.TimesCalled);
        }

        [Fact]
        public void RequestsAreCoalescedWhenReplacingExpiredValue()
        {
            var cache = new LoadingCache<string, string>(valueGenerator.GetNextValue,
                TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500));
            cache.Set("key", "old");
            Thread.Sleep(110);
            var tasks = new Task[3];
            for (var i = 0; i < 3; i++)
            {
                tasks[i] = Task.Run(() => cache.Get("key"));
            }
            Task.WaitAll(tasks);
            Assert.Equal(1, valueGenerator.TimesCalled);
        }

        private class TestValueGenerator
        {
            public volatile int TimesCalled = 0;
            public TimeSpan? Delay = null;

            public String GetNextValue(string key)
            {
                int n = Interlocked.Increment(ref TimesCalled);
                if (Delay != null)
                {
                    Thread.Sleep(Delay.Value);
                }
                return key + "_value_" + n;
            }
        }
    }
}
