using System.Collections.Generic;
using LaunchDarkly.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class TestUtils
    {
        public static void AssertJsonEqual(JToken expected, JToken actual)
        {
            if (!JToken.DeepEquals(expected, actual))
            {
                Assert.True(false,
                    string.Format("JSON result mismatch; expected {0}, got {1}",
                        JsonConvert.SerializeObject(expected),
                        JsonConvert.SerializeObject(actual)));
            }
        }

        public static string TestFilePath(string name)
        {
            return "./TestFiles/" + name;
        }

        // this just lets us avoid deprecation warnings
        public static InMemoryFeatureStore InMemoryFeatureStore()
        {
#pragma warning disable 0618
            return new InMemoryFeatureStore();
#pragma warning restore 0618
        }

        public static IFeatureStoreFactory SpecificFeatureStore(IFeatureStore store)
        {
            return new SpecificFeatureStoreFactory(store);
        }

        public static IEventProcessorFactory SpecificEventProcessor(IEventProcessor ep)
        {
            return new SpecificEventProcessorFactory(ep);
        }

        public static IDataSourceFactory SpecificDataSource(IDataSource up)
        {
            return new SpecificDataSourceFactory(up);
        }

        public static IDataSourceFactory DataSourceWithData(
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> data)
        {
            return new DataSourceFactoryWithData(data);
        }
    }

    public class SpecificFeatureStoreFactory : IFeatureStoreFactory
    {
        private readonly IFeatureStore _store;

        public SpecificFeatureStoreFactory(IFeatureStore store)
        {
            _store = store;
        }

        IFeatureStore IFeatureStoreFactory.CreateFeatureStore()
        {
            return _store;
        }
    }

    public class SpecificEventProcessorFactory : IEventProcessorFactory
    {
        private readonly IEventProcessor _ep;

        public SpecificEventProcessorFactory(IEventProcessor ep)
        {
            _ep = ep;
        }

        IEventProcessor IEventProcessorFactory.CreateEventProcessor(Configuration config)
        {
            return _ep;
        }
    }

    public class SpecificDataSourceFactory : IDataSourceFactory
    {
        private readonly IDataSource _up;

        public SpecificDataSourceFactory(IDataSource up)
        {
            _up = up;
        }

        IDataSource IDataSourceFactory.CreateDataSource(Configuration config, IFeatureStore featureStore)
        {
            return _up;
        }
    }

    public class DataSourceFactoryWithData : IDataSourceFactory
    {
        private readonly IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> _data;

        public DataSourceFactoryWithData(
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> data)
        {
            _data = data;
        }

        public IDataSource CreateDataSource(Configuration config, IFeatureStore featureStore)
        {
            return new DataSourceWithData(featureStore, _data);
        }
    }

    public class DataSourceWithData : IDataSource
    {
        private readonly IFeatureStore _store;
        private readonly IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> _data;

        public DataSourceWithData(IFeatureStore store,
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> data)
        {
            _store = store;
            _data = data;
        }

        public Task<bool> Start()
        {
            _store.Init(_data);
            return Task.FromResult(true);
        }

        public bool Initialized()
        {
            return true;
        }
        
        public void Dispose() { }
    }

    public class TestEventProcessor : IEventProcessor
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
