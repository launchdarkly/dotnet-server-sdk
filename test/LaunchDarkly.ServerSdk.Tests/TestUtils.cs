using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataSources;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using System.Threading.Tasks;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using LaunchDarkly.Sdk.Internal.Events;

namespace LaunchDarkly.Sdk.Server
{
    public class TestUtils
    {
        public static readonly Logger NullLogger = Logs.None.Logger("");

#pragma warning disable 1998
        public static async Task CompletedTask() { } // Task.CompletedTask isn't supported in .NET Framework 4.5.x
#pragma warning restore 1998

        public static string TestFilePath(string name) => "./TestFiles/" + name;

        internal static ItemDescriptor DescriptorOf(FeatureFlag item) => new ItemDescriptor(item.Version, item);

        internal static ItemDescriptor DescriptorOf(Segment item) => new ItemDescriptor(item.Version, item);

        internal static bool UpsertFlag(IDataStore store, FeatureFlag item) =>
            store.Upsert(DataModel.Features, item.Key, DescriptorOf(item));

        internal static bool UpsertSegment(IDataStore store, Segment item) =>
            store.Upsert(DataModel.Segments, item.Key, DescriptorOf(item));

        public static IDataStoreFactory SpecificDataStore(IDataStore store) =>
            new SpecificDataStoreFactory(store);

        public static IEventProcessorFactory SpecificEventProcessor(LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor ep) =>
            new SpecificEventProcessorFactory(ep);

        public static IDataSourceFactory SpecificDataSource(IDataSource up) =>
            new SpecificDataSourceFactory(up);

        public static IPersistentDataStoreFactory ArbitraryPersistentDataStore =>
            new SpecificPersistentDataStoreFactory(new MockCoreSync());

        internal static IDataSourceFactory DataSourceWithData(FullDataSet<ItemDescriptor> data) =>
            new DataSourceFactoryWithData(data);

        internal static DataSourceUpdatesImpl BasicDataSourceUpdates(IDataStore dataStore, Logger logger) =>
            new DataSourceUpdatesImpl(
                dataStore,
                new DataStoreStatusProviderImpl(dataStore, new DataStoreUpdatesImpl(new TaskExecutor(logger))),
                new TaskExecutor(logger),
                logger,
                null
                );

        // Ensures that a data set is sorted by namespace and then by key
        internal static FullDataSet<ItemDescriptor> NormalizeDataSet(FullDataSet<ItemDescriptor> data)
        {
            return new FullDataSet<ItemDescriptor>(
                data.Data.OrderBy(kindAndItems => kindAndItems.Key.Name)
                    .Select(kindAndItems => new KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>(
                        kindAndItems.Key,
                        new KeyedItems<ItemDescriptor>(
                            kindAndItems.Value.Items.OrderBy(keyAndItem => keyAndItem.Key)
                            )
                        )
                    )
                );
        }
    }

    public class SpecificDataStoreFactory : IDataStoreFactory
    {
        private readonly IDataStore _store;

        public SpecificDataStoreFactory(IDataStore store)
        {
            _store = store;
        }

        IDataStore IDataStoreFactory.CreateDataStore(LdClientContext context, IDataStoreUpdates _) => _store;
    }

    public class SpecificEventProcessorFactory : IEventProcessorFactory
    {
        private readonly LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor _ep;

        public SpecificEventProcessorFactory(LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor ep)
        {
            _ep = ep;
        }

        LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor IEventProcessorFactory.CreateEventProcessor(LdClientContext context) => _ep;
    }

    public class SpecificDataSourceFactory : IDataSourceFactory
    {
        private readonly IDataSource _ds;

        public SpecificDataSourceFactory(IDataSource ds)
        {
            _ds = ds;
        }

        IDataSource IDataSourceFactory.CreateDataSource(LdClientContext context, IDataSourceUpdates dataSourceUpdates) => _ds;
    }

    public class DataSourceFactoryWithData : IDataSourceFactory
    {
        private readonly FullDataSet<ItemDescriptor> _data;

        public DataSourceFactoryWithData(FullDataSet<ItemDescriptor> data)
        {
            _data = data;
        }

        public IDataSource CreateDataSource(LdClientContext context, IDataSourceUpdates dataSourceUpdates) =>
            new DataSourceWithData(dataSourceUpdates, _data);
    }

    public class SpecificPersistentDataStoreFactory : IPersistentDataStoreFactory
    {
        private readonly IPersistentDataStore _store;

        public SpecificPersistentDataStoreFactory(IPersistentDataStore store)
        {
            _store = store;
        }

        public IPersistentDataStore CreatePersistentDataStore(LdClientContext context) => _store;

        public IPersistentDataStore CreatePersistentDataStore()
        {
            throw new NotImplementedException();
        }
    }

    public class DataSourceWithData : IDataSource
    {
        private readonly IDataSourceUpdates _dataSourceUpdates;
        private readonly FullDataSet<ItemDescriptor> _data;

        public DataSourceWithData(IDataSourceUpdates dataSourceUpdates, FullDataSet<ItemDescriptor> data)
        {
            _dataSourceUpdates = dataSourceUpdates;
            _data = data;
        }

        public Task<bool> Start()
        {
            _dataSourceUpdates.Init(_data);
            return Task.FromResult(true);
        }

        public bool Initialized => true;
        
        public void Dispose() { }
    }

    public class CapturingDataSourceUpdates : IDataSourceUpdates
    {
        public readonly BlockingCollection<FullDataSet<ItemDescriptor>> Inits =
            new BlockingCollection<FullDataSet<ItemDescriptor>>();
        public readonly BlockingCollection<UpsertParams> Upserts =
            new BlockingCollection<UpsertParams>();
        public DataSourceState State;

        public IDataStoreStatusProvider DataStoreStatusProvider => throw new NotImplementedException();

        public bool Init(FullDataSet<ItemDescriptor> allData)
        {
            Inits.Add(allData);
            return true;
        }

        public void UpdateStatus(DataSourceState newState, DataSourceStatus.ErrorInfo? newError)
        {
            State = newState;
        }

        public bool Upsert(DataKind kind, string key, ItemDescriptor item)
        {
            Upserts.Add(new UpsertParams { Kind = kind, Key = key, Item = item });
            return true;
        }
    }

    public struct UpsertParams
    {
        public DataKind Kind { get; set; }
        public string Key { get; set; }
        public ItemDescriptor Item { get; set; }
    }

    public class CapturingDataStoreFactory : IDataStoreFactory
    {
        private readonly IDataStoreFactory _factory;
        public volatile LdClientContext Context;
        public volatile IDataStoreUpdates DataStoreUpdates;

        public CapturingDataStoreFactory(IDataStoreFactory factory)
        {
            _factory = factory;
        }

        public IDataStore CreateDataStore(LdClientContext context, IDataStoreUpdates dataStoreUpdates)
        {
            Context = context;
            DataStoreUpdates = dataStoreUpdates;
            return _factory.CreateDataStore(context, dataStoreUpdates);
        }
    }

    public class TestEventProcessor : IEventProcessor
    {
        public List<object> Events = new List<object>();

        public void SetOffline(bool offline) { }

        public void Flush() { }

        public void Dispose() { }

        public void RecordEvaluationEvent(EventProcessorTypes.EvaluationEvent e) =>
            Events.Add(e);

        public void RecordIdentifyEvent(EventProcessorTypes.IdentifyEvent e) =>
            Events.Add(e);

        public void RecordCustomEvent(EventProcessorTypes.CustomEvent e) =>
            Events.Add(e);

        public void RecordAliasEvent(EventProcessorTypes.AliasEvent e) =>
            Events.Add(e);
    }

    public class TestEventSender : LaunchDarkly.Sdk.Internal.Events.IEventSender
    {
        public BlockingCollection<Params> Calls = new BlockingCollection<Params>();

        public void Dispose() { }

        public struct Params
        {
            public EventDataKind Kind;
            public string Data;
            public int EventCount;
        }

        public Task<EventSenderResult> SendEventDataAsync(EventDataKind kind, string data, int eventCount)
        {
            Calls.Add(new Params { Kind = kind, Data = data, EventCount = eventCount });
            return Task.FromResult(new EventSenderResult(DeliveryStatus.Succeeded, null));
        }

        public Params RequirePayload()
        {
            Params result;
            if (!Calls.TryTake(out result, TimeSpan.FromSeconds(5)))
            {
                throw new System.Exception("did not receive an event payload");
            }
            return result;
        }

        public void RequireNoPayloadSent(TimeSpan timeout)
        {
            Params result;
            if (Calls.TryTake(out result, timeout))
            {
                throw new System.Exception("received an unexpected event payload");
            }
        }
    }

    public class EventSink<T>
    {
        private readonly BlockingCollection<T> _queue = new BlockingCollection<T>();

        public void Add(object sender, T args) => _queue.Add(args);

        public T ExpectValue() => ExpectValue(TimeSpan.FromSeconds(1));

        public T ExpectValue(TimeSpan timeout)
        {
            if (!_queue.TryTake(out var value, timeout))
            {
                Assert.True(false, "expected an event but did not get one at " + TestLogging.TimestampString);
            }
            return value;
        }

        public bool TryTakeValue(out T value)
        {
            return _queue.TryTake(out value, TimeSpan.FromSeconds(1));
        }

        public void ExpectNoValue() => ExpectNoValue(TimeSpan.FromMilliseconds(100));

        public void ExpectNoValue(TimeSpan timeout)
        {
            if (_queue.TryTake(out _, timeout))
            {
                Assert.False(true, "expected no event but got one at " + TestLogging.TimestampString);
            }
        }
    }
}
