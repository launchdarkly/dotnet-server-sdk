using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.Sdk.Server.Integrations;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.DataStores.DataStoreTestTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    // These tests verify the behavior of CachingStoreWrapper against an underlying mock
    // data store implementation; the test subclasses provide either a sync or async version
    // of the mock. Most of the tests are run twice ([Theory]), once with caching enabled
    // and once not; a few of the tests are only relevant when caching is enabled and so are
    // run only once ([Fact]).
    public abstract class PersistentStoreWrapperTestBase<T> where T : MockCoreBase
    {
        protected T _core;

        // the following are strings instead of enums because Xunit's InlineData can't use enums
        const string Uncached = "Uncached";
        const string Cached = "Cached";
        const string CachedIndefinitely = "CachedIndefinitely";

        private static readonly Exception FakeError = new NotImplementedException("sorry");
        
        protected PersistentStoreWrapperTestBase(T core)
        {
            _core = core;
        }

        internal abstract PersistentStoreWrapper MakeWrapperWithCacheConfig(DataStoreCacheConfig config);

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void GetItem(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var key = "flag";
            var itemv1 = new TestItem("itemv1");
            var itemv2 = new TestItem("itemv2");

            _core.ForceSet(TestDataKind, key, 1, itemv1);
            Assert.Equal(wrapper.Get(TestDataKind, key), itemv1.WithVersion(1));

            _core.ForceSet(TestDataKind, key, 2, itemv2);
            var result = wrapper.Get(TestDataKind, key);
            // if cached, we will not see the new underlying value yet
            Assert.Equal(mode == Uncached ? itemv2.WithVersion(2) : itemv1.WithVersion(1), result);
        }

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void GetDeletedItem(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var key = "flag";
            var itemv2 = new TestItem("itemv2");

            _core.ForceSet(TestDataKind, key, 1, null);
            Assert.Equal(wrapper.Get(TestDataKind, key), new ItemDescriptor(1, null));

            _core.ForceSet(TestDataKind, key, 2, itemv2);
            var result = wrapper.Get(TestDataKind, key);
            // if cached, we will not see the new underlying value yet
            Assert.Equal(mode == Uncached ? itemv2.WithVersion(2) : ItemDescriptor.Deleted(1), result);
        }

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void GetMissingItem(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var key = "flag";
            var item = new TestItem("item");

            Assert.Null(wrapper.Get(TestDataKind, key));

            _core.ForceSet(TestDataKind, key, 1, item);
            var result = wrapper.Get(TestDataKind, key);
            if (mode != Uncached)
            {
                Assert.Null(result); // the cache can retain a null result
            }
            else
            {
                Assert.Equal(item.WithVersion(1), result);
            }
        }

        [Fact]
        public void CachedGetUsesValuesFromInit()
        {
            var wrapper = MakeWrapper(Cached);
            var itemA = new TestItem("itemA");
            var itemB = new TestItem("itemB");

            var allData = new TestDataBuilder()
                .Add(TestDataKind, "keyA", 1, itemA)
                .Add(TestDataKind, "keyB", 1, itemA)
                .Build();
            wrapper.Init(allData);

            _core.ForceRemove(TestDataKind, "keyA");

            Assert.Equal(itemA.WithVersion(1), wrapper.Get(TestDataKind, "keyA"));
        }

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void GetAll(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var itemA = new TestItem("itemA");
            var itemB = new TestItem("itemB");

            _core.ForceSet(TestDataKind, "keyA", 1, itemA);
            _core.ForceSet(TestDataKind, "keyB", 2, itemB);

            var items = wrapper.GetAll(TestDataKind).ToDictionary(kv => kv.Key, kv => kv.Value);
            var expected = ImmutableDictionary.Create<string, ItemDescriptor>()
                .Add("keyA", itemA.WithVersion(1))
                .Add("keyB", itemB.WithVersion(2));
            Assert.Equal(expected, items);

            _core.ForceRemove(TestDataKind, "keyB");
            items = wrapper.GetAll(TestDataKind).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (mode != Uncached)
            {
                Assert.Equal(expected, items);
            }
            else
            {
                var expected1 = ImmutableDictionary.Create<string, ItemDescriptor>()
                    .Add("keyA", itemA.WithVersion(1));
                Assert.Equal(expected1, items);
            }
        }

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void GetAllDoesNotRemoveDeletedItems(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var itemA = new TestItem("itemA");

            _core.ForceSet(TestDataKind, "keyA", 1, itemA);
            _core.ForceSet(TestDataKind, "keyB", 2, null); // deleted item

            var items = wrapper.GetAll(TestDataKind).ToDictionary(kv => kv.Key, kv => kv.Value);
            var expected = ImmutableDictionary.Create<string, ItemDescriptor>()
                .Add("keyA", itemA.WithVersion(1))
                .Add("keyB", ItemDescriptor.Deleted(2));
            Assert.Equal(expected, items);
        }

        [Fact]
        public void CachedAllUsesValuesFromInit()
        {
            var wrapper = MakeWrapper(Cached);
            var itemA = new TestItem("itemA");

            var allData = new TestDataBuilder()
                .Add(TestDataKind, "keyA", 1, itemA)
                .Add(TestDataKind, "keyB", 2, null) // deleted item
                .Build();
            wrapper.Init(allData);
            
            _core.ForceRemove(TestDataKind, "keyA");

            var items = wrapper.GetAll(TestDataKind).ToDictionary(kv => kv.Key, kv => kv.Value);
            var expected = ImmutableDictionary.Create<string, ItemDescriptor>()
                .Add("keyA", itemA.WithVersion(1))
                .Add("keyB", ItemDescriptor.Deleted(2));
            Assert.Equal(expected, items);
        }

        [Fact]
        public void CachedAllUsesFreshValuesIfThereHasBeenAnUpdate()
        {
            var wrapper = MakeWrapper(Cached);
            var itemA = new TestItem("itemA");
            var itemB = new TestItem("itemB");

            var allData = new TestDataBuilder()
                .Add(TestDataKind, "keyA", 1, itemA)
                .Add(TestDataKind, "keyB", 2, itemB)
                .Build();
            wrapper.Init(allData);
            
            // make a change to itemA via the wrapper - this should flush the cache
            var itemAv2 = new TestItem("itemAv2");
            wrapper.Upsert(TestDataKind, "keyA", itemAv2.WithVersion(2));

            // make a change to itemB that bypasses the cache
            var itemBv3 = new TestItem("itemBv3");
            _core.ForceSet(TestDataKind, "keyB", 3, itemBv3);

            // we should now see both changes since the cache was flushed
            var items = wrapper.GetAll(TestDataKind).ToDictionary(kv => kv.Key, kv => kv.Value);
            var expected = ImmutableDictionary.Create<string, ItemDescriptor>()
                .Add("keyA", itemAv2.WithVersion(2))
                .Add("keyB", itemBv3.WithVersion(3));
            Assert.Equal(expected, items);
        }

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void UpsertSuccessful(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var key = "flag";
            var itemv1 = new TestItem("itemv1");
            var itemv2 = new TestItem("itemv2");

            wrapper.Upsert(TestDataKind, key, itemv1.WithVersion(1));
            var internalItem = _core.Data[TestDataKind][key];
            Assert.Equal(itemv1.SerializedWithVersion(1), internalItem);

            wrapper.Upsert(TestDataKind, key, new ItemDescriptor(2, itemv2));
            internalItem = _core.Data[TestDataKind][key];
            Assert.Equal(itemv2.SerializedWithVersion(2), internalItem);

            // if we have a cache, verify that the new item is now cached by writing a different value
            // to the underlying data - Get should still return the cached item
            if (mode != Uncached)
            {
                var itemv3 = new TestItem("itemv3");
                _core.ForceSet(TestDataKind, key, 3, itemv3);
            }

            Assert.Equal(itemv2.WithVersion(2), wrapper.Get(TestDataKind, key));
        }

        [Fact]
        public void CachedUpsertUnsuccessful()
        {
            var wrapper = MakeWrapper(Cached);
            var key = "flag";
            var itemv1 = new TestItem("itemv1");
            var itemv2 = new TestItem("itemv2");

            wrapper.Upsert(TestDataKind, key, itemv2.WithVersion(2));
            var internalItem = _core.Data[TestDataKind][key];
            Assert.Equal(itemv2.SerializedWithVersion(2), internalItem);

            wrapper.Upsert(TestDataKind, key, itemv1.WithVersion(1));
            internalItem = _core.Data[TestDataKind][key];
            Assert.Equal(itemv2.SerializedWithVersion(2), internalItem); // value in store remains the same

            var itemv3 = new TestItem("itemv3");
            _core.ForceSet(TestDataKind, key, 3, itemv3); // bypasses cache so we can verify that itemv2 is in the cache

            Assert.Equal(itemv2.WithVersion(2), wrapper.Get(TestDataKind, key));
        }
        
        [Fact]
        public void CachedStoreWithFiniteTtlDoesNotUpdateCacheIfCoreUpdateFails()
        {
            var wrapper = MakeWrapper(Cached);
            var key = "flag";
            var itemv1 = new TestItem("itemv1");
            var itemv2 = new TestItem("itemv2");

            var allData = new TestDataBuilder()
                .Add(TestDataKind, key, 1, itemv1)
                .Build();
            wrapper.Init(allData);

            _core.Error = FakeError;
            Assert.Throws(FakeError.GetType(),
                () => wrapper.Upsert(TestDataKind, key, itemv2.WithVersion(2)));

            _core.Error = null;
            Assert.Equal(itemv1.WithVersion(1), wrapper.Get(TestDataKind, key)); // cache still has old item, same as underlying store
        }

        [Fact]
        public void CachedStoreWithInfiniteTtlUpdatesCacheEvenIfCoreUpdateFails()
        {
            var wrapper = MakeWrapper(CachedIndefinitely);
            var key = "flag";
            var itemv1 = new TestItem("itemv1");
            var itemv2 = new TestItem("itemv2");

            var allData = new TestDataBuilder()
                .Add(TestDataKind, key, 1, itemv1)
                .Build();
            wrapper.Init(allData);

            _core.Error = FakeError;
            Assert.Throws(FakeError.GetType(),
                () => wrapper.Upsert(TestDataKind, key, itemv2.WithVersion(2)));

            _core.Error = null;
            Assert.Equal(itemv2.WithVersion(2), wrapper.Get(TestDataKind, key)); // underlying store has old item but cache has new item
        }

        [Fact]
        public void CachedStoreWithFiniteTtlDoesNotUpdateCacheIfCoreInitFails()
        {
            var wrapper = MakeWrapper(Cached);
            var key = "flag";
            var item = new TestItem("item");

            var allData = new TestDataBuilder()
                .Add(TestDataKind, key, 1, item)
                .Build();
            _core.Error = FakeError;
            Assert.Throws(FakeError.GetType(), () => wrapper.Init(allData));

            _core.Error = null;
            Assert.Empty(wrapper.GetAll(TestDataKind));
        }

        [Fact]
        public void CachedStoreWithInfiniteTtlUpdatesCacheIfCoreInitFails()
        {
            var wrapper = MakeWrapper(CachedIndefinitely);
            var key = "flag";
            var item = new TestItem("item");

            var allData = new TestDataBuilder()
                .Add(TestDataKind, key, 1, item)
                .Build();
            _core.Error = FakeError;
            Assert.Throws(FakeError.GetType(), () => wrapper.Init(allData));

            _core.Error = null;
            var items = wrapper.GetAll(TestDataKind).ToDictionary(kv => kv.Key, kv => kv.Value);
            var expected = ImmutableDictionary.Create<string, ItemDescriptor>()
                .Add(key, item.WithVersion(1));
            Assert.Equal(expected, items);
        }

        [Fact]
        public void CachedStoreWithFiniteTtlRemovesCachedAllDataIfOneItemIsUpdated()
        {
            var wrapper = MakeWrapper(Cached);
            var keyA = "keyA";
            var itemAv1 = new TestItem("itemAv1");
            var itemAv2 = new TestItem("itemAv2");
            var keyB = "keyB";
            var itemBv1 = new TestItem("itemBv1");
            var itemBv2 = new TestItem("itemBv2");

            var allData = new TestDataBuilder()
                .Add(TestDataKind, keyA, 1, itemAv1)
                .Add(TestDataKind, keyB, 1, itemBv1)
                .Build();
            wrapper.Init(allData);

            wrapper.GetAll(TestDataKind); // now the All data is cached

            // do an Upsert for itemA - this should drop the previous All data from the cache
            wrapper.Upsert(TestDataKind, keyA, itemAv2.WithVersion(2));

            // modify itemB directly in the underlying data
            _core.ForceSet(TestDataKind, keyB, 2, itemBv2);

            // now, All should reread the underlying data so we see both changes
            var items = wrapper.GetAll(TestDataKind).ToDictionary(kv => kv.Key, kv => kv.Value);
            var expected = ImmutableDictionary.Create<string, ItemDescriptor>()
                .Add(keyA, itemAv2.WithVersion(2))
                .Add(keyB, itemBv2.WithVersion(2));
            Assert.Equal(expected, items);
        }

        [Fact]
        public void CachedStoreWithInfiniteTtlUpdatesCachedAllDataIfOneItemIsUpdated()
        {
            var wrapper = MakeWrapper(CachedIndefinitely);
            var keyA = "keyA";
            var itemAv1 = new TestItem("itemAv1");
            var itemAv2 = new TestItem("itemAv2");
            var keyB = "keyB";
            var itemBv1 = new TestItem("itemBv1");
            var itemBv2 = new TestItem("itemBv2");

            var allData = new TestDataBuilder()
                .Add(TestDataKind, keyA, 1, itemAv1)
                .Add(TestDataKind, keyB, 1, itemBv1)
                .Build();
            wrapper.Init(allData);
            
            wrapper.GetAll(TestDataKind); // now the All data is cached

            // do an Upsert for itemA - this should update the underlying data *and* the cached All data
            wrapper.Upsert(TestDataKind, keyA, itemAv2.WithVersion(2));

            // modify itemB directly in the underlying data
            _core.ForceSet(TestDataKind, keyB, 2, itemBv2);

            // now, All should *not* reread the underlying data - we should only see the change to itemA
            var items = wrapper.GetAll(TestDataKind).ToDictionary(kv => kv.Key, kv => kv.Value);
            var expected = ImmutableDictionary.Create<string, ItemDescriptor>()
                .Add(keyA, itemAv1.WithVersion(2))
                .Add(keyB, itemBv1.WithVersion(1));
            Assert.Equal(expected, items);
        }

        private PersistentStoreWrapper MakeWrapper(string mode)
        {
            DataStoreCacheConfig config;
            switch (mode)
            {
                case Cached:
                    config = DataStoreCacheConfig.Enabled.WithTtlSeconds(30);
                    break;
                case CachedIndefinitely:
                    config = DataStoreCacheConfig.Enabled.WithTtl(System.Threading.Timeout.InfiniteTimeSpan);
                    break;
                default:
                    config = DataStoreCacheConfig.Disabled;
                    break;
            }
            return MakeWrapperWithCacheConfig(config);
        }
    }
    
    public class MockCoreBase : IDisposable
    {
        public IDictionary<DataKind, IDictionary<string, SerializedItemDescriptor>> Data =
            new Dictionary<DataKind, IDictionary<string, SerializedItemDescriptor>>();
        public bool Inited;
        public int InitedQueryCount;
        public Exception Error;

        public void Dispose() { }


        public SerializedItemDescriptor? Get(DataKind kind, string key)
        {
            MaybeThrowError();
            if (Data.TryGetValue(kind, out var items))
            {
                if (items.TryGetValue(key, out var item))
                {
                    return item;
                }
            }
            return null;
        }

        public IEnumerable<KeyValuePair<string, SerializedItemDescriptor>> GetAll(DataKind kind)
        {
            MaybeThrowError();
            if (Data.TryGetValue(kind, out var items))
            {
                return items.ToImmutableDictionary();
            }
            return ImmutableDictionary<string, SerializedItemDescriptor>.Empty;
        }

        public void Init(FullDataSet<SerializedItemDescriptor> allData)
        {
            MaybeThrowError();
            Data.Clear();
            foreach (var e in allData.Data)
            {
                Data[e.Key] = e.Value.ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            Inited = true;
        }

        public bool Upsert(DataKind kind, string key, SerializedItemDescriptor item)
        {
            MaybeThrowError();
            if (!Data.ContainsKey(kind))
            {
                Data[kind] = new Dictionary<string, SerializedItemDescriptor>();
            }
            if (Data[kind].TryGetValue(key, out var oldItem))
            {
                if (oldItem.Version >= item.Version)
                {
                    return false;
                }
            }
            Data[kind][key] = item;
            return true;
        }

        public bool Initialized()
        {
            MaybeThrowError();
            ++InitedQueryCount;
            return Inited;
        }

        public void ForceSet(DataKind kind, string key, int version, object item)
        {
            if (!Data.ContainsKey(kind))
            {
                Data[kind] = new Dictionary<string, SerializedItemDescriptor>();
            }
            Data[kind][key] = new SerializedItemDescriptor(version,
                item is null ? null : kind.Serialize(item));
        }

        public void ForceRemove(DataKind kind, string key)
        {
            if (Data.ContainsKey(kind))
            {
                Data[kind].Remove(key);
            }
        }

        private void MaybeThrowError()
        {
            if (Error != null)
            {
                throw Error;
            }
        }
    }
}
