using System;
using System.Collections.Generic;
using Xunit;

namespace LaunchDarkly.Client.Utils.Tests
{
    // These tests verify the behavior of CachingStoreWrapper against an underlying mock
    // data store implementation; the test subclasses provide either a sync or async version
    // of the mock. Most of the tests are run twice ([Theory]), once with caching enabled
    // and once not; a few of the tests are only relevant when caching is enabled and so are
    // run only once ([Fact]).
    public abstract class CachingStoreWrapperTestBase<T> where T : MockCoreBase
    {
        protected T _core;

        // the following are strings instead of enums because Xunit's InlineData can't use enums
        const string Uncached = "Uncached";
        const string Cached = "Cached";
        const string CachedIndefinitely = "CachedIndefinitely";

        private static readonly Exception FakeError = new NotImplementedException("sorry");
        
        protected CachingStoreWrapperTestBase(T core)
        {
            _core = core;
        }

        protected abstract CachingStoreWrapperBuilder MakeWrapperBase();

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void GetItem(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var itemv1 = new MockItem("flag", 1, false);
            var itemv2 = new MockItem("flag", 2, false);

            _core.ForceSet(MockItem.Kind, itemv1);
            Assert.Equal(wrapper.Get(MockItem.Kind, itemv1.Key), itemv1);

            _core.ForceSet(MockItem.Kind, itemv2);
            var result = wrapper.Get(MockItem.Kind, itemv1.Key);
            Assert.Equal(mode == Uncached ? itemv2 : itemv1, result); // if cached, we will not see the new underlying value yet
        }

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void GetDeletedItem(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var itemv1 = new MockItem("flag", 1, true);
            var itemv2 = new MockItem("flag", 2, false);

            _core.ForceSet(MockItem.Kind, itemv1);
            Assert.Null(wrapper.Get(MockItem.Kind, itemv1.Key)); // item is filtered out because deleted is true

            _core.ForceSet(MockItem.Kind, itemv2);
            var result = wrapper.Get(MockItem.Kind, itemv1.Key);
            if (mode != Uncached)
            {
                Assert.Null(result); // if cached, we will not see the new underlying value yet
            }
            else
            {
                Assert.Equal(itemv2, result);
            }
        }

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void GetMissingItem(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var item = new MockItem("flag", 1, false);

            Assert.Null(wrapper.Get(MockItem.Kind, item.Key));

            _core.ForceSet(MockItem.Kind, item);
            var result = wrapper.Get(MockItem.Kind, item.Key);
            if (mode != Uncached)
            {
                Assert.Null(result); // the cache can retain a null result
            }
            else
            {
                Assert.Equal(item, result);
            }
        }

        [Fact]
        public void CachedGetUsesValuesFromInit()
        {
            var wrapper = MakeWrapper(Cached);
            var item1 = new MockItem("flag1", 1, false);
            var item2 = new MockItem("flag2", 1, false);

            var allData = MakeData(item1, item2);
            wrapper.Init(allData);

            _core.ForceRemove(MockItem.Kind, item1.Key);

            Assert.Equal(item1, wrapper.Get(MockItem.Kind, item1.Key));
        }

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void GetAll(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var item1 = new MockItem("flag1", 1, false);
            var item2 = new MockItem("flag2", 1, false);

            _core.ForceSet(MockItem.Kind, item1);
            _core.ForceSet(MockItem.Kind, item2);

            var items = wrapper.All(MockItem.Kind);
            var expected = new Dictionary<String, MockItem>()
            {
                { item1.Key, item1 },
                { item2.Key, item2 }
            };
            Assert.Equal(expected, items);

            _core.ForceRemove(MockItem.Kind, item2.Key);
            items = wrapper.All(MockItem.Kind);
            if (mode != Uncached)
            {
                Assert.Equal(expected, items);
            }
            else
            {
                var expected1 = new Dictionary<string, MockItem>()
                {
                    { item1.Key, item1 }
                };
                Assert.Equal(expected1, items);
            }
        }

        [Theory]
        [InlineData(Uncached)]
        [InlineData(Cached)]
        [InlineData(CachedIndefinitely)]
        public void GetAllRemovesDeletedItems(string mode)
        {
            var wrapper = MakeWrapper(mode);
            var item1 = new MockItem("flag1", 1, false);
            var item2 = new MockItem("flag2", 1, true);

            _core.ForceSet(MockItem.Kind, item1);
            _core.ForceSet(MockItem.Kind, item2);

            var items = wrapper.All(MockItem.Kind);
            var expected = new Dictionary<String, MockItem>()
            {
                { item1.Key, item1 }
            };
            Assert.Equal(expected, items);
        }

        [Fact]
        public void CachedAllUsesValuesFromInit()
        {
            var wrapper = MakeWrapper(Cached);
            var item1 = new MockItem("flag1", 1, false);
            var item2 = new MockItem("flag2", 1, false);

            var allData = MakeData(item1, item2);
            wrapper.Init(allData);
            var expected = new Dictionary<string, MockItem>()
            {
                { item1.Key, item1 },
                { item2.Key, item2 }
            };

            _core.ForceRemove(MockItem.Kind, item1.Key);

            Assert.Equal(expected, wrapper.All(MockItem.Kind));
        }

        [Fact]
        public void CachedAllUsesFreshValuesIfThereHasBeenAnUpdate()
        {
            var wrapper = MakeWrapper(Cached);
            var key1 = "flag1";
            var item1 = new MockItem(key1, 1, false);
            var item1v2 = new MockItem(key1, 2, false);
            var key2 = "flag2";
            var item2 = new MockItem(key2, 1, false);
            var item2v2 = new MockItem(key2, 2, false);

            var allData = MakeData(item1, item2);
            wrapper.Init(allData);
            
            // make a change to item1 via the wrapper - this should flush the cache
            wrapper.Upsert(MockItem.Kind, item1v2);

            // make a change to item2 that bypasses the cache
            _core.ForceSet(MockItem.Kind, item2v2);

            // we should now see both changes since the cache was flushed
            var items = wrapper.All(MockItem.Kind);
            var expected = new Dictionary<string, MockItem>()
            {
                { key1, item1v2 },
                { key2, item2v2 }
            };
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
            var itemv1 = new MockItem(key, 1, false);
            var itemv2 = new MockItem(key, 2, false);

            wrapper.Upsert(MockItem.Kind, itemv1);
            var internalItem1 = (MockItem)_core.Data[MockItem.Kind][key];
            Assert.Equal(itemv1, internalItem1);

            wrapper.Upsert(MockItem.Kind, itemv2);
            var internalItem2 = (MockItem)_core.Data[MockItem.Kind][key];
            Assert.Equal(itemv2, internalItem2);

            // if we have a cache, verify that the new item is now cached by writing a different value
            // to the underlying data - Get should still return the cached item
            if (mode != Uncached)
            {
                MockItem item1v3 = new MockItem(key, 3, false);
                _core.ForceSet(MockItem.Kind, item1v3);
            }

            Assert.Equal(itemv2, wrapper.Get(MockItem.Kind, key));
        }

        [Fact]
        public void CachedUpsertUnsuccessful()
        {
            var wrapper = MakeWrapper(Cached);
            var key = "flag";
            var itemv1 = new MockItem(key, 1, false);
            var itemv2 = new MockItem(key, 2, false);

            wrapper.Upsert(MockItem.Kind, itemv2);
            var internalItem2 = (MockItem)_core.Data[MockItem.Kind][key];
            Assert.Equal(itemv2, internalItem2);

            wrapper.Upsert(MockItem.Kind, itemv1);
            var internalItem1 = (MockItem)_core.Data[MockItem.Kind][key];
            Assert.Equal(itemv2, internalItem1); // value in store remains the same

            var itemv3 = new MockItem(key, 3, false);
            _core.ForceSet(MockItem.Kind, itemv3); // bypasses cache so we can verify that itemv2 is in the cache

            Assert.Equal(itemv2, wrapper.Get(MockItem.Kind, key));
        }
        
        [Fact]
        public void CachedStoreWithFiniteTtlDoesNotUpdateCacheIfCoreUpdateFails()
        {
            var wrapper = MakeWrapper(Cached);
            var key = "flag";
            var itemv1 = new MockItem(key, 1, false);
            var itemv2 = new MockItem(key, 2, false);

            wrapper.Init(MakeData(itemv1));

            _core.Error = FakeError;
            Assert.Throws(FakeError.GetType(), () => wrapper.Upsert(MockItem.Kind, itemv2));

            _core.Error = null;
            Assert.Equal(itemv1, wrapper.Get(MockItem.Kind, key)); // cache still has old item, same as underlying store
        }

        [Fact]
        public void CachedStoreWithInfiniteTtlUpdatesCacheEvenIfCoreUpdateFails()
        {
            var wrapper = MakeWrapper(CachedIndefinitely);
            var key = "flag";
            var itemv1 = new MockItem(key, 1, false);
            var itemv2 = new MockItem(key, 2, false);

            wrapper.Init(MakeData(itemv1));

            _core.Error = FakeError;
            Assert.Throws(FakeError.GetType(), () => wrapper.Upsert(MockItem.Kind, itemv2));

            _core.Error = null;
            Assert.Equal(itemv2, wrapper.Get(MockItem.Kind, key)); // underlying store has old item but cache has new item
        }

        [Fact]
        public void CachedStoreWithFiniteTtlDoesNotUpdateCacheIfCoreInitFails()
        {
            var wrapper = MakeWrapper(Cached);
            var key = "flag";
            var item = new MockItem(key, 1, false);

            _core.Error = FakeError;
            Assert.Throws(FakeError.GetType(), () => wrapper.Init(MakeData(item)));

            _core.Error = null;
            Assert.Empty(wrapper.All(MockItem.Kind));
        }

        [Fact]
        public void CachedStoreWithInfiniteTtlUpdatesCacheIfCoreInitFails()
        {
            var wrapper = MakeWrapper(CachedIndefinitely);
            var key = "flag";
            var item = new MockItem(key, 1, false);

            _core.Error = FakeError;
            Assert.Throws(FakeError.GetType(), () => wrapper.Init(MakeData(item)));

            _core.Error = null;
            var expected = new Dictionary<string, MockItem> { { key, item } };
            Assert.Equal(expected, wrapper.All(MockItem.Kind));
        }

        [Fact]
        public void CachedStoreWithFiniteTtlRemovesCachedAllDataIfOneItemIsUpdated()
        {
            var wrapper = MakeWrapper(Cached);
            var item1v1 = new MockItem("item1", 1, false);
            var item1v2 = new MockItem("item1", 2, false);
            var item2v1 = new MockItem("item2", 1, false);
            var item2v2 = new MockItem("item2", 2, false);

            wrapper.Init(MakeData(item1v1, item2v1));
            wrapper.All(MockItem.Kind); // now the All data is cached

            // do an Upsert for item1 - this should drop the previous All data from the cache
            wrapper.Upsert(MockItem.Kind, item1v2);

            // modify item2 directly in the underlying data
            _core.ForceSet(MockItem.Kind, item2v2);

            // now, All should reread the underlying data so we see both changes
            var expected = new Dictionary<string, MockItem> { { item1v1.Key, item1v2 }, { item2v1.Key, item2v2 } };
            Assert.Equal(expected, wrapper.All(MockItem.Kind));
        }

        [Fact]
        public void CachedStoreWithFiniteTtlUpdatesCachedAllDataIfOneItemIsUpdated()
        {
            var wrapper = MakeWrapper(CachedIndefinitely);
            var item1v1 = new MockItem("item1", 1, false);
            var item1v2 = new MockItem("item1", 2, false);
            var item2v1 = new MockItem("item2", 1, false);
            var item2v2 = new MockItem("item2", 2, false);

            wrapper.Init(MakeData(item1v1, item2v1));
            wrapper.All(MockItem.Kind); // now the All data is cached

            // do an Upsert for item1 - this should update the underlying data *and* the cached All data
            wrapper.Upsert(MockItem.Kind, item1v2);

            // modify item2 directly in the underlying data
            _core.ForceSet(MockItem.Kind, item2v2);

            // now, All should *not* reread the underlying data - we should only see the change to item1
            var expected = new Dictionary<string, MockItem> { { item1v1.Key, item1v2 }, { item2v1.Key, item2v1 } };
            Assert.Equal(expected, wrapper.All(MockItem.Kind));
        }

        private CachingStoreWrapper MakeWrapper(string mode)
        {
            FeatureStoreCacheConfig config;
            switch (mode)
            {
                case Cached:
                    config = FeatureStoreCacheConfig.Enabled.WithTtlSeconds(30);
                    break;
                case CachedIndefinitely:
                    config = FeatureStoreCacheConfig.Enabled.WithTtl(System.Threading.Timeout.InfiniteTimeSpan);
                    break;
                default:
                    config = FeatureStoreCacheConfig.Disabled;
                    break;
            }
            return MakeWrapperBase().WithCaching(config).Build();
        }

        private IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> MakeData(params MockItem[] items)
        {
            var innerDict = new Dictionary<string, IVersionedData>();
            foreach (var item in items)
            {
                innerDict[item.Key] = item;
            }
            return new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>()
            {
                { MockItem.Kind, innerDict }
            };
        }
    }

    internal class MockItem : IVersionedData
    {
        public static VersionedDataKind<MockItem> Kind = new MockItemKind();

        public string Key { get; set; }
        public int Version { get; set; }
        public bool Deleted { get; set; }

        public MockItem(string key, int version, bool deleted)
        {
            Key = key;
            Version = version;
            Deleted = deleted;
        }

        public override string ToString()
        {
            return "MockItem(" + Key + "," + Version + "," + Deleted + ")";
        }
    }

    internal class MockItemKind : VersionedDataKind<MockItem>
    {
        override public string GetNamespace()
        {
            return "things";
        }

        override public Type GetItemType()
        {
            return typeof(MockItem);
        }

        public override string GetStreamApiPath()
        {
            return "/things/"; // not used
        }

        public override MockItem MakeDeletedItem(string key, int version)
        {
            return new MockItem(key, version, false);
        }
    }

    public class MockCoreBase : IDisposable
    {
        public IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> Data =
            new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>();
        public bool Inited;
        public int InitedQueryCount;
        public Exception Error;

        public void Dispose() { }


        public IVersionedData GetInternal(IVersionedDataKind kind, string key)
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

        public IDictionary<string, IVersionedData> GetAllInternal(IVersionedDataKind kind)
        {
            MaybeThrowError();
            if (Data.TryGetValue(kind, out var items))
            {
                return new Dictionary<string, IVersionedData>(items);
            }
            return new Dictionary<string, IVersionedData>();
        }

        public void InitInternal(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData)
        {
            MaybeThrowError();
            Data.Clear();
            foreach (var e in allData)
            {
                Data[e.Key] = new Dictionary<string, IVersionedData>(e.Value);
            }
            Inited = true;
        }

        public IVersionedData UpsertInternal(IVersionedDataKind kind, IVersionedData item)
        {
            MaybeThrowError();
            if (!Data.ContainsKey(kind))
            {
                Data[kind] = new Dictionary<string, IVersionedData>();
            }
            if (Data[kind].TryGetValue(item.Key, out var oldItem))
            {
                if (oldItem.Version >= item.Version)
                {
                    return oldItem;
                }
            }
            Data[kind][item.Key] = item;
            return item;
        }

        public bool InitializedInternal()
        {
            MaybeThrowError();
            ++InitedQueryCount;
            return Inited;
        }

        public void ForceSet(IVersionedDataKind kind, IVersionedData item)
        {
            if (!Data.ContainsKey(kind))
            {
                Data[kind] = new Dictionary<string, IVersionedData>();
            }
            Data[kind][item.Key] = item;
        }

        public void ForceRemove(IVersionedDataKind kind, string key)
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
