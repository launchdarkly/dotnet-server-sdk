using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Interfaces;
using LaunchDarkly.Sdk.Server.Interfaces;
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

        public static void AssertJsonEqual(LdValue expected, LdValue actual)
        {
            if (!expected.Equals(actual))
            {
                if (expected.Type != LdValueType.Object || actual.Type != LdValueType.Object)
                {
                    Assert.Equal(expected, actual); // generates standard failure message
                }
                // generate a better message with a diff of properties
                var expectedDict = expected.AsDictionary(LdValue.Convert.Json);
                var actualDict = actual.AsDictionary(LdValue.Convert.Json);
                var allKeys = expectedDict.Keys.Union(actualDict.Keys);
                var lines = new List<string>();
                foreach (var key in allKeys)
                {
                    string expectedDesc = null, actualDesc = null;
                    if (expectedDict.ContainsKey(key))
                    {
                        if (actualDict.ContainsKey(key))
                        {
                            if (!expectedDict[key].Equals(actualDict[key]))
                            {
                                expectedDesc = expectedDict[key].ToJsonString();
                                actualDesc = actualDict[key].ToJsonString();
                            }
                        }
                        else
                        {
                            expectedDesc = expectedDict[key].ToJsonString();
                            actualDesc = "<absent>";
                        }
                    }
                    else
                    {
                        actualDesc = actualDict[key].ToJsonString();
                        expectedDesc = "<absent>";
                    }
                    if (expectedDesc != null || actualDesc != null)
                    {
                        lines.Add(string.Format("property \"{0}\": expected = {1}, actual = {2}",
                            key, expectedDesc, actualDesc));
                    }
                }
                Assert.True(false, "JSON result mismatch:\n" + string.Join("\n", lines));
            }
        }

        public static string TestFilePath(string name) => "./TestFiles/" + name;
        
        internal static bool UpsertFlag(IDataStore store, FeatureFlag item)
        {
            return store.Upsert(DataKinds.Features, item.Key, new ItemDescriptor(item.Version, item));
        }

        internal static bool UpsertSegment(IDataStore store, Segment item)
        {
            return store.Upsert(DataKinds.Segments, item.Key, new ItemDescriptor(item.Version, item));
        }

        public static IDataStoreFactory SpecificDataStore(IDataStore store) =>
            new SpecificDataStoreFactory(store);

        public static IEventProcessorFactory SpecificEventProcessor(LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor ep) =>
            new SpecificEventProcessorFactory(ep);

        public static IDataSourceFactory SpecificDataSource(IDataSource up) =>
            new SpecificDataSourceFactory(up);

        internal static IDataSourceFactory DataSourceWithData(FullDataSet<ItemDescriptor> data) =>
            new DataSourceFactoryWithData(data);
    }

    public class SpecificDataStoreFactory : IDataStoreFactory
    {
        private readonly IDataStore _store;

        public SpecificDataStoreFactory(IDataStore store)
        {
            _store = store;
        }

        IDataStore IDataStoreFactory.CreateDataStore(LdClientContext context) => _store;
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

        public bool Initialized() => true;
        
        public void Dispose() { }
    }

    public class CapturingDataSourceFactory : IDataSourceFactory
    {
        public volatile LdClientContext Context;
        public volatile IDataSourceUpdates DataSourceUpdates;
        private readonly Func<Task<bool>> _startFn;

        public CapturingDataSourceFactory(Func<Task<bool>> startFn = null)
        {
            _startFn = startFn;
        }

        public IDataSource CreateDataSource(LdClientContext context, IDataSourceUpdates dataSourceUpdates)
        {
            Context = context;
            DataSourceUpdates = dataSourceUpdates;
            return new DataSourceImpl(_startFn);
        }

        private class DataSourceImpl : IDataSource
        {
            internal readonly Func<Task<bool>> _startFn;

            internal DataSourceImpl(Func<Task<bool>> startFn)
            {
                _startFn = startFn;
            }

            public Task<bool> Start() => _startFn is null ? Task.FromResult(true) : _startFn();

            public bool Initialized() => true;

            public void Dispose() { }
        }
    }

    public class TestEventProcessor : LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor
    {
        public List<Event> Events = new List<Event>();

        public void SendEvent(Event e)
        {
            Events.Add(e);
        }

        public void SetOffline(bool offline) { }

        public void Flush() { }

        public void Dispose() { }
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
