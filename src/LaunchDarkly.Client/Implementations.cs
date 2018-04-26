﻿using System;
using System.Collections.Generic;
using System.Text;
using Common.Logging;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Provides factories for the standard implementations of LaunchDarkly component interfaces.
    /// </summary>
    public static class Implementations
    {
        private static IFeatureStoreFactory _inMemoryFeatureStoreFactory = new InMemoryFeatureStoreFactory();
        private static IEventProcessorFactory _eventProcessorFactory = new DefaultEventProcessorFactory();
        private static IEventProcessorFactory _nullEventProcessorFactory = new NullEventProcessorFactory();
        private static IUpdateProcessorFactory _updateProcessorFactory = new DefaultUpdateProcessorFactory();
        private static IUpdateProcessorFactory _nullUpdateProcessorFactory = new NullUpdateProcessorFactory();
        
        /// <summary>
        /// Returns a factory for the default in-memory implementation of <see cref="IFeatureStore"/>.
        /// </summary>
        public static IFeatureStoreFactory InMemoryFeatureStore
        {
            get
            {
                return _inMemoryFeatureStoreFactory;
            }
        }

        /// <summary>
        /// Returns a factory for the default implementation of <see cref="IStoreEvents"/>, which
        /// forwards all analytics events to LaunchDarkly (unless the client is offline).
        /// </summary>
        public static IEventProcessorFactory DefaultEventProcessor
        {
            get
            {
                return _eventProcessorFactory;
            }
        }

        /// <summary>
        /// Returns a factory for a null implementation of <see cref="IStoreEvents"/>, which will
        /// discard all analytics events and not send them to LaunchDarkly, regardless of any
        /// other configuration.
        /// </summary>
        public static IEventProcessorFactory NullEventProcessor
        {
            get
            {
                return _nullEventProcessorFactory;
            }
        }

        /// <summary>
        /// Returns a factory for the default implementation of <see cref="IUpdateProcessor"/>, which
        /// receives feature flag data from LaunchDarkly using either streaming or polling as configured
        /// (or does nothing if the client is offline, or in LDD mode).
        /// </summary>
        public static IUpdateProcessorFactory DefaultUpdateProcessor
        {
            get
            {
                return _updateProcessorFactory;
            }
        }

        /// <summary>
        /// Returns a factory for a null implementation of <see cref="IUpdateProcessor"/>, which
        /// does not connect to LaunchDarkly, regardless of any other configuration.
        /// </summary>
        public static IUpdateProcessorFactory NullUpdateProcessor
        {
            get
            {
                return _nullUpdateProcessorFactory;
            }
        }
    }

    internal class DefaultEventProcessorFactory : IEventProcessorFactory
    {
        IEventProcessor IEventProcessorFactory.CreateEventProcessor(Configuration config)
        {
            if (config.Offline)
            {
                return new NullEventProcessor();
            }
            else
            {
                return new DefaultEventProcessor(config);
            }
        }
    }

    internal class NullEventProcessorFactory : IEventProcessorFactory
    {
        IEventProcessor IEventProcessorFactory.CreateEventProcessor(Configuration config)
        {
            return new NullEventProcessor();
        }
    }

    internal class InMemoryFeatureStoreFactory : IFeatureStoreFactory
    {
        IFeatureStore IFeatureStoreFactory.CreateFeatureStore()
        {
            return new InMemoryFeatureStore();
        }
    }

    internal class DefaultUpdateProcessorFactory : IUpdateProcessorFactory
    {
        // Note, logger uses LDClient class name for backward compatibility
        private static readonly ILog Log = LogManager.GetLogger(typeof(LdClient));

        IUpdateProcessor IUpdateProcessorFactory.CreateUpdateProcessor(Configuration config, IFeatureStore featureStore)
        {
            if (config.Offline)
            {
                Log.Info("Starting Launchdarkly client in offline mode.");
                return new NullUpdateProcessor();
            }
            else
            {
                FeatureRequestor requestor = new FeatureRequestor(config);
                if (config.IsStreamingEnabled)
                {
                    return new StreamProcessor(config, requestor, featureStore);
                }
                else
                {
                    Log.Warn("You should only disable the streaming API if instructed to do so by LaunchDarkly support");
                    return new PollingProcessor(config, requestor, featureStore);
                }
            }
        }
    }

    internal class NullUpdateProcessorFactory : IUpdateProcessorFactory
    {
        IUpdateProcessor IUpdateProcessorFactory.CreateUpdateProcessor(Configuration config, IFeatureStore featureStore)
        {
            return new NullUpdateProcessor();
        }
    }
}
