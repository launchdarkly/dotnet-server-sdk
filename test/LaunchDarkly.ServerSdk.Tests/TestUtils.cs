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

        public static IUpdateProcessorFactory SpecificUpdateProcessor(IUpdateProcessor up)
        {
            return new SpecificUpdateProcessorFactory(up);
        }

        public static IUpdateProcessorFactory UpdateProcessorWithData(
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> data)
        {
            return new UpdateProcessorFactoryWithData(data);
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

    public class SpecificUpdateProcessorFactory : IUpdateProcessorFactory
    {
        private readonly IUpdateProcessor _up;

        public SpecificUpdateProcessorFactory(IUpdateProcessor up)
        {
            _up = up;
        }

        IUpdateProcessor IUpdateProcessorFactory.CreateUpdateProcessor(Configuration config, IFeatureStore featureStore)
        {
            return _up;
        }
    }

    public class UpdateProcessorFactoryWithData : IUpdateProcessorFactory
    {
        private readonly IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> _data;

        public UpdateProcessorFactoryWithData(
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> data)
        {
            _data = data;
        }

        public IUpdateProcessor CreateUpdateProcessor(Configuration config, IFeatureStore featureStore)
        {
            return new UpdateProcessorWithData(featureStore, _data);
        }
    }

    public class UpdateProcessorWithData : IUpdateProcessor
    {
        private readonly IFeatureStore _store;
        private readonly IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> _data;

        public UpdateProcessorWithData(IFeatureStore store,
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

        public void Flush() { }

        public void Dispose() { }
    }
}
