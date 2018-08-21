using System.Collections.Generic;
using LaunchDarkly.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class TestUtils
    {
        public static  void AssertJsonEqual(JToken expected, JToken  actual)
        {
            if (!JToken.DeepEquals(expected, actual))
            {
                Assert.True(false,
                    string.Format("JSON result mismatch; expected {0}, got {1}",
                        JsonConvert.SerializeObject(expected),
                        JsonConvert.SerializeObject(actual)));
            }
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
