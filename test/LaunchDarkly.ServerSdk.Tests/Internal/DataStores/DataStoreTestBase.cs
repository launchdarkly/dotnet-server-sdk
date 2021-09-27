using System.Collections.Generic;
using System.Linq;
using Xunit;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.DataStores.DataStoreTestTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    public abstract class DataStoreTestBase
    {
        protected IDataStore store;

        internal static readonly TestItem item1 = new TestItem("item1");
        internal const string item1Key = "key1";
        internal const int item1Version = 10;
        internal static readonly TestItem item2 = new TestItem("item2");
        internal const string item2Key = "key2";
        internal const int item2Version = 11;

        protected void InitStore()
        {
            var allData = new TestDataBuilder()
                .Add(TestDataKind, item1Key, item1Version, item1)
                .Add(TestDataKind, item2Key, item2Version, item2)
                .Build();
            store.Init(allData);
        }
        
        [Fact]
        public void StoreInitializedAfterInit()
        {
            InitStore();
            Assert.True(store.Initialized());
        }

        [Fact]
        public void GetExistingitem()
        {
            InitStore();
            var result = store.Get(TestDataKind, item1Key);
            Assert.Equal(result, new ItemDescriptor(item1Version, item1));
        }

        [Fact]
        public void GetNonexistingItem()
        {
            InitStore();
            var result = store.Get(TestDataKind, "biz");
            Assert.Null(result);
        }

        [Fact]
        public void GetAllItems()
        {
            InitStore();
            var result = store.GetAll(TestDataKind);
            Assert.Equal(2, result.Items.Count());
            Assert.Contains(KeyAndItemDescriptor(item1Key, item1Version, item1), result.Items);
            Assert.Contains(KeyAndItemDescriptor(item2Key, item2Version, item2), result.Items);
        }

        [Fact]
        public void GetAllItemsWithDeletedItems()
        {
            InitStore();
            store.Upsert(TestDataKind, item1Key, new ItemDescriptor(item1Version + 1, null));
            var result = store.GetAll(TestDataKind);
            Assert.Equal(2, result.Items.Count());
            Assert.Contains(KeyAndItemDescriptor(item1Key, item1Version + 1, null), result.Items);
            Assert.Contains(KeyAndItemDescriptor(item2Key, item2Version, item2), result.Items);
        }

        [Fact]
        public void GetAllUnknownKind()
        {
            InitStore();
            var result = store.GetAll(OtherDataKind);
            Assert.Empty(result.Items);
        }

        [Fact]
        public void UpsertWithNewerVersion()
        {
            InitStore();
            var newItem1 = new TestItem("item1-updated");
            var newVersion = item1Version + 1;

            var success = store.Upsert(TestDataKind, item1Key, new ItemDescriptor(newVersion, newItem1));
            Assert.True(success);

            var result = store.Get(TestDataKind, item1Key);
            Assert.True(result.HasValue);
            Assert.Equal(newVersion, result.Value.Version);
            Assert.Equal(newItem1, result.Value.Item);
        }

        [Fact]
        public void UpsertWithOlderVersion()
        {
            InitStore();
            var newItem1 = new TestItem("item1-updated");
            var newVersion = item1Version - 1;

            var success = store.Upsert(TestDataKind, item1Key, new ItemDescriptor(newVersion, newItem1));
            Assert.False(success);

            var result = store.Get(TestDataKind, item1Key);
            Assert.True(result.HasValue);
            Assert.Equal(item1Version, result.Value.Version);
            Assert.Equal(item1, result.Value.Item);
        }

        [Fact]
        public void UpsertNewItem()
        {
            InitStore();
            var newItem = new TestItem("item3");
            var newKey = "key3";
            var newVersion = 22;

            var success = store.Upsert(TestDataKind, newKey, new ItemDescriptor(newVersion, newItem));
            Assert.True(success);

            var result = store.Get(TestDataKind, newKey);
            Assert.True(result.HasValue);
            Assert.Equal(newVersion, result.Value.Version);
            Assert.Equal(newItem, result.Value.Item);
        }

        [Fact]
        public void UpsertNewKind()
        {
            InitStore();
            var newItem = new TestItem("item-of-other-kind");
            var newVersion = 1;

            var success = store.Upsert(OtherDataKind, item1Key, new ItemDescriptor(newVersion, newItem));
            Assert.True(success);

            var result = store.Get(OtherDataKind, item1Key);
            Assert.True(result.HasValue);
            Assert.Equal(newVersion, result.Value.Version);
            Assert.Equal(newItem, result.Value.Item);

            result = store.Get(TestDataKind, item1Key);
            Assert.True(result.HasValue);
            Assert.Equal(item1Version, result.Value.Version);
            Assert.Equal(item1, result.Value.Item);
        }

        [Fact]
        public void DeleteWithNewerVersion()
        {
            InitStore();
            var newVersion = item1Version + 1;

            var success = store.Upsert(TestDataKind, item1Key, new ItemDescriptor(newVersion, null));
            Assert.True(success);

            var result = store.Get(TestDataKind, item1Key);
            Assert.True(result.HasValue);
            Assert.Equal(newVersion, result.Value.Version);
            Assert.Null(result.Value.Item);
        }

        [Fact]
        public void DeleteWithOlderVersion()
        {
            InitStore();
            var newVersion = item1Version - 1;

            var success = store.Upsert(TestDataKind, item1Key, new ItemDescriptor(newVersion, null));
            Assert.False(success);

            var result = store.Get(TestDataKind, item1Key);
            Assert.True(result.HasValue);
            Assert.Equal(item1Version, result.Value.Version);
            Assert.Equal(item1, result.Value.Item);
        }

        [Fact]
        public void DeleteUnknownItem()
        {
            InitStore();
            var newKey = "key3";
            var newVersion = 22;

            var success = store.Upsert(TestDataKind, newKey, new ItemDescriptor(newVersion, null));
            Assert.True(success);

            var result = store.Get(TestDataKind, newKey);
            Assert.True(result.HasValue);
            Assert.Equal(newVersion, result.Value.Version);
            Assert.Null(result.Value.Item);
        }

        [Fact]
        public void DeleteUnknownKind()
        {
            InitStore();
            var newVersion = 1;

            var success = store.Upsert(OtherDataKind, item1Key, new ItemDescriptor(newVersion, null));
            Assert.True(success);

            var result = store.Get(OtherDataKind, item1Key);
            Assert.True(result.HasValue);
            Assert.Equal(newVersion, result.Value.Version);
            Assert.Null(result.Value.Item);

            result = store.Get(TestDataKind, item1Key);
            Assert.True(result.HasValue);
            Assert.Equal(item1Version, result.Value.Version);
            Assert.Equal(item1, result.Value.Item);
        }

        [Fact]
        public void UpsertOlderVersionAfterDelete()
        {
            InitStore();
            var deletedVersion = item1Version + 1;

            var success = store.Upsert(TestDataKind, item1Key, new ItemDescriptor(deletedVersion, null));
            Assert.True(success);

            var newItem = "item1a";
            success = store.Upsert(TestDataKind, item1Key, new ItemDescriptor(item1Version, newItem));
            Assert.False(success);
            
            var result = store.Get(TestDataKind, item1Key);
            Assert.True(result.HasValue);
            Assert.Equal(deletedVersion, result.Value.Version);
            Assert.Null(result.Value.Item);
        }

        private KeyValuePair<string, ItemDescriptor> KeyAndItemDescriptor(string key, int version, object item)
        {
            return new KeyValuePair<string, ItemDescriptor>(key, new ItemDescriptor(version, item));
        }
    }
}
