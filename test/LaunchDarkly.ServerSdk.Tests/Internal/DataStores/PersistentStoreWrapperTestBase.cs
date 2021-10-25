using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.TestHelpers;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.DataStores.DataStoreTestTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    // These tests verify the behavior of CachingStoreWrapper against an underlying mock
    // data store implementation; the test subclasses provide either a sync or async version
    // of the mock. Most of the tests are run twice ([Theory]), once with caching enabled
    // and once not; a few of the tests are only relevant when caching is enabled and so are
    // run only once ([Fact]).
    public abstract class PersistentStoreWrapperTestBase<T> : BaseTest where T : MockCoreBase
    {
        // We need a longer timeout for calling ExpectValue() in the context of a previously failed data
        // store becoming available again, because the status update in that scenario comes from a polling
        // task that deliberately does not run super fast.
        private static readonly TimeSpan TimeoutForStatusUpdateWhenStoreBecomesAvailableAgain =
            TimeSpan.FromSeconds(2);

        protected T _core;
        internal DataStoreUpdatesImpl _dataStoreUpdates;

        internal class CacheMode
        {
            internal string Name { get; set; }
            internal TimeSpan Ttl { get; set; }
            public override string ToString() => Name;
            internal bool IsCached => Ttl != TimeSpan.Zero;
            internal bool IsUncached => Ttl == TimeSpan.Zero;
            internal bool IsCachedIndefinitely => Ttl < TimeSpan.Zero;
            internal DataStoreCacheConfig CacheConfig =>
                Ttl == TimeSpan.Zero ?
                    DataStoreCacheConfig.Disabled :
                    DataStoreCacheConfig.Enabled.WithTtl(Ttl);
        }

        internal class PersistMode
        {
            internal bool PersistOnlyAsString { get; set; }
            public override string ToString() => PersistOnlyAsString ? "PersistOnlyAsString" : "PersistWithMetadata";
        }

        public class TestParams
        {
            internal CacheMode CacheMode { get; set; }
            internal PersistMode PersistMode { get; set; }

            public override string ToString() => "(" + CacheMode + "," + PersistMode + ")";
        }

        internal static readonly CacheMode Uncached = new CacheMode { Name = "Uncached", Ttl = TimeSpan.Zero };
        internal static readonly CacheMode Cached = new CacheMode { Name = "Cached", Ttl = TimeSpan.FromSeconds(30) };
        internal static readonly CacheMode CachedIndefinitely = new CacheMode { Name = "CachedIndefinitely", Ttl = Timeout.InfiniteTimeSpan };

        // PersistOnlyAsString means we're simulating a persistent store that can only hold one string
        // value per item, with no other metadata - like Redis. This affects the logic for updates and
        // deleted items.
        internal static readonly PersistMode PersistOnlyAsString = new PersistMode { PersistOnlyAsString = true };

        // PersistWithMetadata means we're simulating a persistent store that's able to hold the version
        // number and deletion status separately fro the string value.
        internal static readonly PersistMode PersistWithMetadata = new PersistMode { PersistOnlyAsString = false };

        public static IEnumerable<object[]> AllTestParams()
        {
            foreach (var cacheMode in new CacheMode[] { Uncached, Cached, CachedIndefinitely })
            {
                foreach (var persistMode in new PersistMode[] { PersistWithMetadata, PersistOnlyAsString })
                {
                    yield return new object[] { new TestParams { CacheMode = cacheMode, PersistMode = persistMode } };
                }
            }
        }

        private static readonly Exception FakeError = new NotImplementedException("sorry");
        
        protected PersistentStoreWrapperTestBase(T core, ITestOutputHelper testOutput) : base(testOutput)
        {
            _core = core;
            _dataStoreUpdates = new DataStoreUpdatesImpl(BasicTaskExecutor, TestLogger);
        }

        internal abstract PersistentStoreWrapper MakeWrapper(TestParams testParams);

        [Theory]
        [MemberData(nameof(AllTestParams))]
        public void GetItem(TestParams testParams)
        {
            var wrapper = MakeWrapper(testParams);
            var key = "flag";
            var itemv1 = new TestItem("itemv1");
            var itemv2 = new TestItem("itemv2");

            _core.ForceSet(TestDataKind, key, 1, itemv1);
            Assert.Equal(itemv1.WithVersion(1), wrapper.Get(TestDataKind, key));

            _core.ForceSet(TestDataKind, key, 2, itemv2);
            var result = wrapper.Get(TestDataKind, key);
            // if cached, we will not see the new underlying value yet
            Assert.Equal(testParams.CacheMode.IsUncached ? itemv2.WithVersion(2) : itemv1.WithVersion(1), result);
        }

        [Theory]
        [MemberData(nameof(AllTestParams))]
        public void GetDeletedItem(TestParams testParams)
        {
            var wrapper = MakeWrapper(testParams);
            var key = "flag";
            var itemv2 = new TestItem("itemv2");

            _core.ForceSet(TestDataKind, key, 1, null);
            Assert.Equal(new ItemDescriptor(1, null), wrapper.Get(TestDataKind, key));

            _core.ForceSet(TestDataKind, key, 2, itemv2);
            var result = wrapper.Get(TestDataKind, key);
            // if cached, we will not see the new underlying value yet
            Assert.Equal(testParams.CacheMode.IsUncached ? itemv2.WithVersion(2) : ItemDescriptor.Deleted(1), result);
        }

        [Theory]
        [MemberData(nameof(AllTestParams))]
        public void GetMissingItem(TestParams testParams)
        {
            var wrapper = MakeWrapper(testParams);
            var key = "flag";
            var item = new TestItem("item");

            Assert.Null(wrapper.Get(TestDataKind, key));

            _core.ForceSet(TestDataKind, key, 1, item);
            var result = wrapper.Get(TestDataKind, key);
            if (testParams.CacheMode.IsCached)
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
        [MemberData(nameof(AllTestParams))]
        public void GetAll(TestParams testParams)
        {
            var wrapper = MakeWrapper(testParams);
            var itemA = new TestItem("itemA");
            var itemB = new TestItem("itemB");

            _core.ForceSet(TestDataKind, "keyA", 1, itemA);
            _core.ForceSet(TestDataKind, "keyB", 2, itemB);

            var items = wrapper.GetAll(TestDataKind).Items.ToDictionary(kv => kv.Key, kv => kv.Value);
            var expected = ImmutableDictionary.Create<string, ItemDescriptor>()
                .Add("keyA", itemA.WithVersion(1))
                .Add("keyB", itemB.WithVersion(2));
            Assert.Equal(expected, items);

            _core.ForceRemove(TestDataKind, "keyB");
            items = wrapper.GetAll(TestDataKind).Items.ToDictionary(kv => kv.Key, kv => kv.Value);
            if (testParams.CacheMode.IsCached)
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
        [MemberData(nameof(AllTestParams))]
        public void GetAllDoesNotRemoveDeletedItems(TestParams testParams)
        {
            var wrapper = MakeWrapper(testParams);
            var itemA = new TestItem("itemA");

            _core.ForceSet(TestDataKind, "keyA", 1, itemA);
            _core.ForceSet(TestDataKind, "keyB", 2, null); // deleted item

            var items = wrapper.GetAll(TestDataKind).Items.ToDictionary(kv => kv.Key, kv => kv.Value);
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

            var items = wrapper.GetAll(TestDataKind).Items.ToDictionary(kv => kv.Key, kv => kv.Value);
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
            var items = wrapper.GetAll(TestDataKind).Items.ToDictionary(kv => kv.Key, kv => kv.Value);
            var expected = ImmutableDictionary.Create<string, ItemDescriptor>()
                .Add("keyA", itemAv2.WithVersion(2))
                .Add("keyB", itemBv3.WithVersion(3));
            Assert.Equal(expected, items);
        }

        [Theory]
        [MemberData(nameof(AllTestParams))]
        public void UpsertSuccessful(TestParams testParams)
        {
            var wrapper = MakeWrapper(testParams);
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
            if (testParams.CacheMode.IsCached)
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
            Assert.Empty(wrapper.GetAll(TestDataKind).Items);
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
            var items = wrapper.GetAll(TestDataKind).Items.ToDictionary(kv => kv.Key, kv => kv.Value);
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
            var items = wrapper.GetAll(TestDataKind).Items.ToDictionary(kv => kv.Key, kv => kv.Value);
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
            var items = wrapper.GetAll(TestDataKind).Items.ToDictionary(kv => kv.Key, kv => kv.Value);
            var expected = ImmutableDictionary.Create<string, ItemDescriptor>()
                .Add(keyA, itemAv2.WithVersion(2))
                .Add(keyB, itemBv1.WithVersion(1));
            Assert.Equal(expected, items);
        }

        [Fact]
        public void StatusIsOkInitially()
        {
            using (var wrapper = MakeWrapper(Cached))
            {
                var dataStoreStatusProvider = new DataStoreStatusProviderImpl(wrapper, _dataStoreUpdates);
                var status = dataStoreStatusProvider.Status;
                Assert.True(status.Available);
            }
        }

        [Fact]
        public void StatusIsUnavailableAfterError()
        {
            using (var wrapper = MakeWrapper(Cached))
            {
                var dataStoreStatusProvider = new DataStoreStatusProviderImpl(wrapper, _dataStoreUpdates);

                CauseStoreError(_core, wrapper);

                var status = dataStoreStatusProvider.Status;
                Assert.False(status.Available);
                Assert.False(status.RefreshNeeded);
            }
        }

        [Theory]
        [MemberData(nameof(AllTestParams))]
        public void StatusListenerIsNotifiedOnFailureAndRecovery(TestParams testParams)
        {
            using (var wrapper = MakeWrapper(testParams))
            {
                var dataStoreStatusProvider = new DataStoreStatusProviderImpl(wrapper, _dataStoreUpdates);

                var statuses = new EventSink<DataStoreStatus>();
                dataStoreStatusProvider.StatusChanged += statuses.Add;

                CauseStoreError(_core, wrapper);

                var status1 = statuses.ExpectValue();
                Assert.False(status1.Available);
                Assert.False(status1.RefreshNeeded);

                AssertLogMessageRegex(true, LogLevel.Warn, "Detected persistent store unavailability");

                MakeStoreAvailable(_core);

                var status2 = statuses.ExpectValue(TimeoutForStatusUpdateWhenStoreBecomesAvailableAgain);
                Assert.True(status2.Available);
                Assert.Equal(!testParams.CacheMode.IsCachedIndefinitely, status2.RefreshNeeded);

                AssertLogMessageRegex(true, LogLevel.Warn, "Persistent store is available again");
            }
        }

        [Fact]
        public void CacheIsWrittenToStoreAfterRecoveryIfTtlIsInfinite()
        {
            using (var wrapper = MakeWrapper(CachedIndefinitely))
            {
                var dataStoreStatusProvider = new DataStoreStatusProviderImpl(wrapper, _dataStoreUpdates);

                var statuses = new EventSink<DataStoreStatus>();
                dataStoreStatusProvider.StatusChanged += statuses.Add;

                string key1 = "key1", key2 = "key2";
                var item1 = new TestItem("name1");
                var item2 = new TestItem("name2");

                wrapper.Init(new TestDataBuilder()
                    .Add(TestDataKind, key1, 1, item1)
                    .Build());

                // In infinite cache mode, we do *not* expect exceptions thrown by the store to be propagated; it will
                // swallow the error, but also go into polling/recovery mode. Note that even though the store rejects
                // this update, it'll still be cached.
                CauseStoreError(_core, wrapper);
                Assert.Equal(FakeError, Assert.Throws(FakeError.GetType(),
                    () => wrapper.Upsert(TestDataKind, key1, item1.WithVersion(2))));

                Assert.Equal(item1.WithVersion(2), wrapper.Get(TestDataKind, key1));

                var status1 = statuses.ExpectValue();
                Assert.False(status1.Available);
                Assert.False(status1.RefreshNeeded);

                // While the store is still down, try to update it again - the update goes into the cache

                Assert.Equal(FakeError, Assert.Throws(FakeError.GetType(),
                    () => wrapper.Upsert(TestDataKind, key2, item2.WithVersion(1))));

                Assert.Equal(item2.WithVersion(1), wrapper.Get(TestDataKind, key2));

                // Verify that this update did not go into the underlying data yet

                Assert.False(_core.Data[TestDataKind].TryGetValue(key2, out _));

                // Now simulate the store coming back up
                MakeStoreAvailable(_core);

                // Wait for the poller to notice this and publish a new status
                var status2 = statuses.ExpectValue(TimeoutForStatusUpdateWhenStoreBecomesAvailableAgain);
                Assert.True(status2.Available);
                Assert.False(status2.RefreshNeeded);

                // Once that has happened, the cache should have been written to the store
                Assert.Equal(item1.SerializedWithVersion(2), _core.Data[TestDataKind][key1]);
                Assert.Equal(item2.SerializedWithVersion(1), _core.Data[TestDataKind][key2]);
            }
        }

        [Fact]
        public void StatusRemainsUnavailableIfStoreSaysItIsAvailableButInitFails()
        {
            // Most of this test is identical to CacheIsWrittenToStoreAfterRecoveryIfTtlIsInfinite() except as noted below.
            using (var wrapper = MakeWrapper(CachedIndefinitely))
            {
                var dataStoreStatusProvider = new DataStoreStatusProviderImpl(wrapper, _dataStoreUpdates);

                var statuses = new EventSink<DataStoreStatus>();
                dataStoreStatusProvider.StatusChanged += statuses.Add;

                string key1 = "key1", key2 = "key2";
                var item1 = new TestItem("name1");
                var item2 = new TestItem("name2");

                wrapper.Init(new TestDataBuilder()
                    .Add(TestDataKind, key1, 1, item1)
                    .Build());

                // In infinite cache mode, we do *not* expect exceptions thrown by the store to be propagated; it will
                // swallow the error, but also go into polling/recovery mode. Note that even though the store rejects
                // this update, it'll still be cached.
                CauseStoreError(_core, wrapper);
                Assert.Equal(FakeError, Assert.Throws(FakeError.GetType(),
                    () => wrapper.Upsert(TestDataKind, key1, item1.WithVersion(2))));

                Assert.Equal(item1.WithVersion(2), wrapper.Get(TestDataKind, key1));

                var status1 = statuses.ExpectValue();
                Assert.False(status1.Available);
                Assert.False(status1.RefreshNeeded);

                // While the store is still down, try to update it again - the update goes into the cache

                Assert.Equal(FakeError, Assert.Throws(FakeError.GetType(),
                    () => wrapper.Upsert(TestDataKind, key2, item2.WithVersion(1))));

                Assert.Equal(item2.WithVersion(1), wrapper.Get(TestDataKind, key2));

                // Verify that this update did not go into the underlying data yet

                Assert.False(_core.Data[TestDataKind].TryGetValue(key2, out _));

                // Here's what is unique to this test: we are telling the store to report its status as "available",
                // but *not* clearing the fake exception, so when the poller tries to write the cached data with
                // init() it should fail.
                _core.Available = true;

                // We can't prove that an unwanted status transition will never happen, but we can verify that it
                // does not happen within two status poll intervals.
                Thread.Sleep(PersistentDataStoreStatusManager.PollInterval + PersistentDataStoreStatusManager.PollInterval);

                statuses.ExpectNoValue();
                Assert.InRange(_core.InitCalledCount, 2, 100); // that is, it *tried* to do at least one more init

                // Now simulate the store coming back up and actually working
                _core.Error = null;

                // Wait for the poller to notice this and publish a new status
                var status2 = statuses.ExpectValue(TimeoutForStatusUpdateWhenStoreBecomesAvailableAgain);
                Assert.True(status2.Available);
                Assert.False(status2.RefreshNeeded);

                // Once that has happened, the cache should have been written to the store
                Assert.Equal(item1.SerializedWithVersion(2), _core.Data[TestDataKind][key1]);
                Assert.Equal(item2.SerializedWithVersion(1), _core.Data[TestDataKind][key2]);
            }
        }

        private PersistentStoreWrapper MakeWrapper(CacheMode cacheMode) =>
            MakeWrapper(new TestParams { CacheMode = cacheMode, PersistMode = PersistWithMetadata });

        private void CauseStoreError(MockCoreBase core, PersistentStoreWrapper wrapper)
        {
            core.Available = false;
            core.Error = FakeError;
            Assert.Equal(FakeError, Assert.Throws(FakeError.GetType(), () =>
                wrapper.Upsert(TestDataKind, "irrelevant-key", ItemDescriptor.Deleted(1))));
        }

        private void MakeStoreAvailable(MockCoreBase core)
        {
            core.Error = null;
            core.Available = true;
        }
    }
    
    public class MockCoreBase : IDisposable
    {
        public IDictionary<DataKind, IDictionary<string, SerializedItemDescriptor>> Data =
            new Dictionary<DataKind, IDictionary<string, SerializedItemDescriptor>>();
        public bool PersistOnlyAsString = false;
        public bool Inited;
        public int InitedQueryCount;
        public int InitCalledCount;
        public Exception Error;
        public bool Available = true;

        public void Dispose() { }


        public SerializedItemDescriptor? Get(DataKind kind, string key)
        {
            MaybeThrowError();
            if (Data.TryGetValue(kind, out var items))
            {
                if (items.TryGetValue(key, out var item))
                {
                    if (PersistOnlyAsString)
                    {
                        // This simulates the kind of store implementation that can't track metadata separately
                        return new SerializedItemDescriptor(0, false, item.SerializedItem);
                    }
                    return item;
                }
            }
            return null;
        }

        public KeyedItems<SerializedItemDescriptor> GetAll(DataKind kind)
        {
            MaybeThrowError();
            if (Data.TryGetValue(kind, out var items))
            {
                return new KeyedItems<SerializedItemDescriptor>(items.ToImmutableDictionary());
            }
            return KeyedItems<SerializedItemDescriptor>.Empty();
        }

        public void Init(FullDataSet<SerializedItemDescriptor> allData)
        {
            InitCalledCount++;
            MaybeThrowError();
            Data.Clear();
            foreach (var e in allData.Data)
            {
                var kind = e.Key;
                Data[kind] = e.Value.Items.ToDictionary(kv => kv.Key, kv => StorableItem(kind, kv.Value));
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
                // If PersistOnlyAsString is true, simulate the kind of implementation where we can't see the
                // version as a separate attribute in the database and must deserialize the item to get it.
                var oldVersion = PersistOnlyAsString ?
                    kind.Deserialize(oldItem.SerializedItem).Version :
                    oldItem.Version;
                if (oldVersion >= item.Version)
                {
                    return false;
                }
            }
            Data[kind][key] = StorableItem(kind, item);
            return true;
        }

        public bool Initialized()
        {
            MaybeThrowError();
            ++InitedQueryCount;
            return Inited;
        }

        public bool IsStoreAvailable()
        {
            return Available;
        }

        public void ForceSet(DataKind kind, string key, int version, object item)
        {
            if (!Data.ContainsKey(kind))
            {
                Data[kind] = new Dictionary<string, SerializedItemDescriptor>();
            }
            var serializedItemDesc = new SerializedItemDescriptor(version,
                item is null, kind.Serialize(new ItemDescriptor(version, item)));
            Data[kind][key] = StorableItem(kind, serializedItemDesc);
        }

        public void ForceRemove(DataKind kind, string key)
        {
            if (Data.ContainsKey(kind))
            {
                Data[kind].Remove(key);
            }
        }

        private SerializedItemDescriptor StorableItem(DataKind kind, SerializedItemDescriptor item)
        {
            if (item.Deleted && !PersistOnlyAsString)
            {
                // This simulates the kind of store implementation that *can* track metadata separately, so we don't
                // have to persist the placeholder string for deleted items
                return new SerializedItemDescriptor(item.Version, true, null);
            }
            return item;
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
