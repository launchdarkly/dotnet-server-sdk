using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.Client;

namespace LaunchDarkly.Tests
{
    public class TestUtils
    {
        public static IFeatureStoreFactory SpecificFeatureStore(IFeatureStore store)
        {
            return new SpecificFeatureStoreFactory(store);
        }

        public static IEventProcessorFactory SpecificEventProcessor(IEventProcessor ep)
        {
            return new SpecificEventProcessorFactory(ep);
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
}
