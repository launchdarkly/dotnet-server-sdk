using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using LaunchDarkly.Client;

namespace LaunchDarkly.Tests
{
    public abstract class FeatureStoreTestBase
    {
        protected IFeatureStore store;

        internal FeatureFlag feature1 = MakeFeature("foo", 10);
        internal FeatureFlag feature2 = MakeFeature("bar", 10);

        protected void InitStore()
        {
            IDictionary<string, IVersionedData> items = new Dictionary<string, IVersionedData>();
            items[feature1.Key] = feature1;
            items[feature2.Key] = feature2;
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData =
                new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>();
            allData[VersionedDataKind.Features] = items;
            store.Init(allData);
        }

        internal static FeatureFlag MakeFeature(string key, int version)
        {
            return new FeatureFlagBuilder(key).Version(version).Build();
        }

        internal static FeatureFlag CopyFeatureWithVersion(FeatureFlag old, int newVersion)
        {
            return new FeatureFlagBuilder(old).Version(newVersion).Build();
        }

        [Fact]
        public void StoreInitializedAfterInit()
        {
            InitStore();
            Assert.True(store.Initialized());
        }

        [Fact]
        public void GetExistingFeature()
        {
            InitStore();
            var result = store.Get(VersionedDataKind.Features, feature1.Key);
            Assert.Equal(feature1.Key, result.Key);
        }

        [Fact]
        public void GetNonexistingFeature()
        {
            InitStore();
            var result = store.Get(VersionedDataKind.Features, "biz");
            Assert.Null(result);
        }

        [Fact]
        public void GetAllFeatures()
        {
            InitStore();
            var result = store.All(VersionedDataKind.Features);
            Assert.Equal(2, result.Count);
            Assert.Equal(feature1.Key, result[feature1.Key].Key);
            Assert.Equal(feature2.Key, result[feature2.Key].Key);
        }

        [Fact]
        public void UpsertWithNewerVersion()
        {
            InitStore();
            var newVer = CopyFeatureWithVersion(feature1, feature1.Version + 1);
            store.Upsert(VersionedDataKind.Features, newVer);
            var result = store.Get(VersionedDataKind.Features, feature1.Key);
            Assert.Equal(newVer.Version, result.Version);
        }

        [Fact]
        public void UpsertWithOlderVersion()
        {
            InitStore();
            var newVer = CopyFeatureWithVersion(feature1, feature1.Version - 1);
            store.Upsert(VersionedDataKind.Features, newVer);
            var result = store.Get(VersionedDataKind.Features, feature1.Key);
            Assert.Equal(feature1.Version, result.Version);
        }

        [Fact]
        public void UpsertNewFeature()
        {
            InitStore();
            var newFeature = MakeFeature("biz", 99);
            store.Upsert(VersionedDataKind.Features, newFeature);
            var result = store.Get(VersionedDataKind.Features, newFeature.Key);
            Assert.Equal(newFeature.Key, result.Key);
        }

        [Fact]
        public void DeleteWithNewerVersion()
        {
            InitStore();
            store.Delete(VersionedDataKind.Features, feature1.Key, feature1.Version + 1);
            Assert.Null(store.Get(VersionedDataKind.Features, feature1.Key));
        }

        [Fact]
        public void DeleteWithOlderVersion()
        {
            InitStore();
            store.Delete(VersionedDataKind.Features, feature1.Key, feature1.Version - 1);
            Assert.NotNull(store.Get(VersionedDataKind.Features, feature1.Key));
        }

        [Fact]
        public void DeleteUnknownFeature()
        {
            InitStore();
            store.Delete(VersionedDataKind.Features, "biz", 11);
            Assert.Null(store.Get(VersionedDataKind.Features, "biz"));
        }

        [Fact]
        public void UpsertOlderVersionAfterDelete()
        {
            InitStore();
            store.Delete(VersionedDataKind.Features, feature1.Key, feature1.Version + 1);
            store.Upsert(VersionedDataKind.Features, feature1);
            Assert.Null(store.Get(VersionedDataKind.Features, feature1.Key));
        }
    }
}
