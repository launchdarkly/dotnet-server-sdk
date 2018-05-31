using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Common.Logging;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// A client for the LaunchDarkly API. Client instances are thread-safe. Applications should instantiate
    /// a single <c>LdClient</c> for the lifetime of their application.
    /// </summary>
    public sealed class LdClient : IDisposable, ILdClient
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LdClient));

        private readonly Configuration _configuration;
        private readonly IEventProcessor _eventProcessor;
        private readonly IFeatureStore _featureStore;
        private readonly IUpdateProcessor _updateProcessor;
        private readonly EventFactory _eventFactory = EventFactory.Default;
        private bool _shouldDisposeEventProcessor;
        private bool _shouldDisposeFeatureStore;

        /// <summary>
        /// Creates a new client to connect to LaunchDarkly with a custom configuration, and a custom
        /// implementation of the analytics event processor.
        /// 
        /// This constructor is deprecated; please use
        /// <see cref="ConfigurationExtensions.WithEventProcessorFactory(Configuration, IEventProcessorFactory)"/>
        /// instead.
        /// </summary>
        /// <param name="config">a client configuration object</param>
        /// <param name="eventProcessor">an event processor</param>
        [Obsolete("Deprecated, please use Configuration.WithEventProcessorFactory")]
        public LdClient(Configuration config, IEventProcessor eventProcessor)
        {
            Log.InfoFormat("Starting LaunchDarkly Client {0}",
                ServerSideClientEnvironment.Instance.Version);

            _configuration = config;

            if (eventProcessor == null)
            {
                _eventProcessor = (_configuration.EventProcessorFactory ??
                    Components.DefaultEventProcessor).CreateEventProcessor(_configuration);
                _shouldDisposeEventProcessor = true;
            }
            else
            {
                _eventProcessor = eventProcessor;
                // The following line is for backward compatibility with the obsolete mechanism by which the
                // caller could pass in an IStoreEvents implementation instance that we did not create.  We
                // were not disposing of that instance when the client was closed, so we should continue not
                // doing so until the next major version eliminates that mechanism.  We will always dispose
                // of instances that we created ourselves from a factory.
                _shouldDisposeEventProcessor = false;
            }
            
            if (_configuration.FeatureStore == null)
            {
                _featureStore = (_configuration.FeatureStoreFactory ??
                    Components.InMemoryFeatureStore).CreateFeatureStore();
                _shouldDisposeFeatureStore = true;
            }
            else
            {
                _featureStore = _configuration.FeatureStore;
                _shouldDisposeFeatureStore = false; // see previous comment
            }

            _updateProcessor = (_configuration.UpdateProcessorFactory ??
                Components.DefaultUpdateProcessor).CreateUpdateProcessor(_configuration, _featureStore);

            var initTask = _updateProcessor.Start();

            if (!(_updateProcessor is NullUpdateProcessor))
            {
                Log.InfoFormat("Waiting up to {0} milliseconds for LaunchDarkly client to start..",
                    _configuration.StartWaitTime.TotalMilliseconds);
            }

            var unused = initTask.Wait(_configuration.StartWaitTime);
        }

        /// <summary>
        /// Creates a new client to connect to LaunchDarkly with a custom configuration. This constructor
        /// can be used to configure advanced client features, such as customizing the LaunchDarkly base URL.
        /// </summary>
        /// <param name="config">a client configuration object</param>
        #pragma warning disable 618  // suppress warning for calling obsolete ctor
        public LdClient(Configuration config) : this(config, null)
        #pragma warning restore 618
        {
        }

        /// <summary>
        /// Creates a new client instance that connects to LaunchDarkly with the default configuration. In most
        /// cases, you should use this constructor.
        /// </summary>
        /// <param name="sdkKey">the SDK key for your LaunchDarkly environment</param>
        public LdClient(string sdkKey) : this(Configuration.Default(sdkKey))
        {
        }

        /// <see cref="ILdClient.Initialized"/>
        public bool Initialized()
        {
            return IsOffline() || _updateProcessor.Initialized();
        }

        /// <see cref="ILdClient.IsOffline"/>
        public bool IsOffline()
        {
            return _configuration.Offline;
        }

        /// <see cref="ILdClient.BoolVariation(string, User, bool)"/>
        public bool BoolVariation(string key, User user, bool defaultValue = false)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.Boolean);
            return value.Value<bool>();
        }

        /// <see cref="ILdClient.IntVariation(string, User, int)"/>
        public int IntVariation(string key, User user, int defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.Integer);
            return value.Value<int>();
        }

        /// <see cref="ILdClient.FloatVariation(string, User, float)"/>
        public float FloatVariation(string key, User user, float defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.Float);
            return value.Value<float>();
        }

        /// <see cref="ILdClient.StringVariation(string, User, string)"/>
        public string StringVariation(string key, User user, string defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.String);
            return value.Value<string>();
        }

        /// <see cref="ILdClient.JsonVariation(string, User, JToken)"/>
        public JToken JsonVariation(string key, User user, JToken defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, null);
            return value;
        }

        /// <see cref="ILdClient.AllFlags(User)"/>
        public IDictionary<string, JToken> AllFlags(User user)
        {
            if (IsOffline())
            {
                Log.Warn("AllFlags() was called when client is in offline mode. Returning null.");
                return null;
            }
            if (!Initialized())
            {
                Log.Warn("AllFlags() was called before client has finished initializing. Returning null.");
                return null;
            }
            if (user == null || user.Key == null)
            {
                Log.Warn("AllFlags() called with null user or null user key. Returning null");
                return null;
            }

            IDictionary<string, FeatureFlag> flags = _featureStore.All(VersionedDataKind.Features);
            IDictionary<string, JToken> results = new Dictionary<string, JToken>();
            foreach (KeyValuePair<string, FeatureFlag> pair in flags)
            {
                try
                {
                    FeatureFlag.EvalResult evalResult = pair.Value.Evaluate(user, _featureStore, _eventFactory);
                    results.Add(pair.Key, evalResult.Result);
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("Exception caught when evaluating all flags: {0}", e, Util.ExceptionMessage(e));
                }
            }
            return results;
        }

        private JToken Evaluate(string featureKey, User user, JToken defaultValue, JTokenType? expectedType)
        {
            if (!Initialized())
            {
                Log.Warn("LaunchDarkly client has not yet been initialized. Returning default");
                return defaultValue;
            }
            
            try
            {
                var featureFlag = _featureStore.Get(VersionedDataKind.Features, featureKey);
                if (featureFlag == null)
                {
                    Log.InfoFormat("Unknown feature flag {0}; returning default value",
                        featureKey);

                    _eventProcessor.SendEvent(_eventFactory.NewUnknownFeatureRequestEvent(featureKey, user, defaultValue));
                    return defaultValue;
                }

                if (user == null || user.Key == null)
                {
                    Log.Warn("Feature flag evaluation called with null user or null user key. Returning default");
                    _eventProcessor.SendEvent(_eventFactory.NewDefaultFeatureRequestEvent(featureFlag, user, defaultValue));
                    return defaultValue;
                }
                
                FeatureFlag.EvalResult evalResult = featureFlag.Evaluate(user, _featureStore, _eventFactory);
                if (!IsOffline())
                {
                    foreach (var prereqEvent in evalResult.PrerequisiteEvents)
                    {
                        _eventProcessor.SendEvent(prereqEvent);
                    }
                }
                if (evalResult.Result != null)
                {
                    if (expectedType != null && !evalResult.Result.Type.Equals(expectedType))
                    {
                        Log.ErrorFormat("Expected type: {0} but got {1} when evaluating FeatureFlag: {2}. Returning default",
                            expectedType,
                            evalResult.GetType(),
                            featureKey);

                        _eventProcessor.SendEvent(_eventFactory.NewDefaultFeatureRequestEvent(featureFlag, user, defaultValue));
                        return defaultValue;
                    }
                    _eventProcessor.SendEvent(_eventFactory.NewFeatureRequestEvent(featureFlag, user, evalResult.Variation, evalResult.Result, defaultValue));
                    return evalResult.Result;
                }
                else
                {
                    _eventProcessor.SendEvent(_eventFactory.NewDefaultFeatureRequestEvent(featureFlag, user, defaultValue));
                    return defaultValue;
                }
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Encountered exception in LaunchDarkly client: {0} when evaluating feature key: {1} for user key: {2}",
                     e,
                     Util.ExceptionMessage(e),
                     featureKey,
                     user.Key);

                Log.Debug("{0}", e);
            }
            _eventProcessor.SendEvent(_eventFactory.NewUnknownFeatureRequestEvent(featureKey, user, defaultValue));
            return defaultValue;
        }

        /// <see cref="ILdClient.SecureModeHash(User)"/>
        public string SecureModeHash(User user)
        {
            if (user == null || string.IsNullOrEmpty(user.Key))
            {
                return null;
            }
            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            byte[] keyBytes = encoding.GetBytes(_configuration.SdkKey);

            HMACSHA256 hmacSha256 = new HMACSHA256(keyBytes);
            byte[] hashedMessage = hmacSha256.ComputeHash(encoding.GetBytes(user.Key));
            return BitConverter.ToString(hashedMessage).Replace("-", "").ToLower();
        }

        /// <see cref="ILdClient.Track(string, User, string)"/>
        public void Track(string name, User user, string data)
        {
            if (user == null || user.Key == null)
            {
                Log.Warn("Track called with null user or null user key");
            }
            _eventProcessor.SendEvent(_eventFactory.NewCustomEvent(name, user, data));
        }

        /// <see cref="ILdClient.Identify(User)"/>
        public void Identify(User user)
        {
            if (user == null || user.Key == null)
            {
                Log.Warn("Identify called with null user or null user key");
            }
            _eventProcessor.SendEvent(_eventFactory.NewIdentifyEvent(user));
        }

        /// <see cref="ILdClient.Version"/>
        public Version Version
        {
            get
            {
                return typeof(LdClient).GetTypeInfo().Assembly.GetName().Version;
            }
        }
        
        private void Dispose(bool disposing)
        {
            if (disposing) // follow standard IDisposable pattern
            {
                Log.Info("Closing LaunchDarkly client.");
                // See comments in LdClient constructor: eventually all of these implementation objects
                // will be factory-created and will have the same lifecycle as the client.
                if (_shouldDisposeEventProcessor)
                {
                    _eventProcessor.Dispose();
                }
                if (_shouldDisposeFeatureStore)
                {
                    _featureStore.Dispose();
                }
                _updateProcessor.Dispose();
            }
        }

        /// <summary>
        /// Shuts down the client and releases any resources it is using.
        /// 
        /// Any components that were added by specifying a factory object
        /// (<see cref="ConfigurationExtensions.WithFeatureStore(Configuration, IFeatureStore)"/>, etc.)
        /// will also be disposed of by this method; their lifecycle is the same as the client's.
        /// However, for any components that you constructed yourself and passed in (via the deprecated
        /// method <see cref="ConfigurationExtensions.WithFeatureStore(Configuration, IFeatureStore)"/>,
        /// or the deprecated <c>LdClient</c> constructor that takes an <see cref="IEventProcessor"/>),
        /// this will not happen; you are responsible for managing their lifecycle.
        /// </summary>
        /// <see cref="ILdClient.Dispose"/>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <see cref="ILdClient.Flush"/>
        public void Flush()
        {
            _eventProcessor.Flush();
        }
    }
}