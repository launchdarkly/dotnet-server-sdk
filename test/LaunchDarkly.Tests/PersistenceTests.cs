using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Client;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class PersistenceTests
    {
        private class TestPersistenceStore : InMemoryFeatureStore
        {
            protected override bool IsPersisted => true;
            public string PersistedData { get; set; }

            public int StoreCount { get; set; }
            public int LoadCount { get; set; }
            protected override Task<string> LoadPersistedDataAsync()
            {
                LoadCount++;
                return Task.FromResult(PersistedData);
            }

            protected override Task StorePersistedDataAsync(string data)
            {
                PersistedData = data;
                StoreCount++;
                return Task.FromResult(0);
            }
        }


        [Fact]
        public void PersistedDataShouldRehydrate()
        {
            var testee = new TestPersistenceStore();
            var asFeatureStore = testee as IFeatureStore;
            asFeatureStore.LoadPersistedDataAsync().Wait();
            Assert.Equal(1, testee.LoadCount);
            Assert.Equal(0, testee.StoreCount);

            asFeatureStore.Init(new Dictionary<string, FeatureFlag> { { "test", new FeatureFlag("test", 1, false, new List<Prerequisite>(), "test", new List<Target>(), new List<Rule>(), null, null, null, false) } }, "test");
            Thread.Sleep(100);

            Assert.Equal(1, testee.LoadCount);
            Assert.Equal(1, testee.StoreCount);

            var persistedData = testee.PersistedData;

            testee = new TestPersistenceStore {PersistedData = persistedData};
            asFeatureStore = testee as IFeatureStore;
            asFeatureStore.LoadPersistedDataAsync().Wait();
            Assert.Equal(1, testee.LoadCount);
            Assert.Equal(0, testee.StoreCount);
            Assert.Equal("test", asFeatureStore.VersionIdentifier);
            Assert.NotNull(asFeatureStore.Get("test"));
        }
    }
}
