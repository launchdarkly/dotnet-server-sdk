using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Interfaces;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using System.Threading.Tasks;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

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

        IDataSource IDataSourceFactory.CreateDataSource(LdClientContext context, IDataStoreUpdates dataStoreUpdates) => _ds;
    }

    public class DataSourceFactoryWithData : IDataSourceFactory
    {
        private readonly FullDataSet<ItemDescriptor> _data;

        public DataSourceFactoryWithData(FullDataSet<ItemDescriptor> data)
        {
            _data = data;
        }

        public IDataSource CreateDataSource(LdClientContext context, IDataStoreUpdates dataStoreUpdates) =>
            new DataSourceWithData(dataStoreUpdates, _data);
    }

    public class DataSourceWithData : IDataSource
    {
        private readonly IDataStoreUpdates _storeUpdates;
        private readonly FullDataSet<ItemDescriptor> _data;

        public DataSourceWithData(IDataStoreUpdates storeUpdates, FullDataSet<ItemDescriptor> data)
        {
            _storeUpdates = storeUpdates;
            _data = data;
        }

        public Task<bool> Start()
        {
            _storeUpdates.Init(_data);
            return Task.FromResult(true);
        }

        public bool Initialized() => true;
        
        public void Dispose() { }
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
}
