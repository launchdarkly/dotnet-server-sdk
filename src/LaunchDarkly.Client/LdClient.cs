using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Common.Logging;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// A client for the LaunchDarkly API. Client instances are thread-safe. Applications should instantiate
    /// a single <c>LdClient</c> for the lifetime of their application.
    /// </summary>
    public class LdClient : IDisposable, ILdClient
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LdClient));

        private readonly Configuration _configuration;
        private readonly IStoreEvents _eventStore;
        private readonly IFeatureStore _featureStore;
        private readonly IUpdateProcessor _updateProcessor;

        internal LdClient(Configuration config, IStoreEvents eventStore)
        {
            Log.InfoFormat("Starting LaunchDarkly Client {0}",
                Configuration.Version);

            _configuration = config;
            _eventStore = eventStore;
            _featureStore = _configuration.FeatureStore;

            if (_configuration.Offline)
            {
                Log.Info("Starting Launchdarkly client in offline mode.");
                return;
            }

            var featureRequestor = new FeatureRequestor(config);

            if (_configuration.IsStreamingEnabled)
            {
                _updateProcessor = new StreamProcessor(config, featureRequestor, _featureStore);
            }
            else
            {
                Log.Warn("You should only disable the streaming API if instructed to do so by LaunchDarkly support");
                _updateProcessor = new PollingProcessor(config, featureRequestor, _featureStore);
            }
            var initTask = _updateProcessor.Start();

            Log.InfoFormat("Waiting up to {0} milliseconds for LaunchDarkly client to start..",
                _configuration.StartWaitTime.TotalMilliseconds);

            var unused = initTask.Wait(_configuration.StartWaitTime);
        }

        /// <summary>
        /// Creates a new client to connect to LaunchDarkly with a custom configuration. This constructor
        /// can be used to configure advanced client features, such as customizing the LaunchDarkly base URL.
        /// </summary>
        /// <param name="config">a client configuration object</param>
        public LdClient(Configuration config) : this(config, new EventProcessor(config))
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
                    FeatureFlag.EvalResult evalResult = pair.Value.Evaluate(user, _featureStore, _configuration);
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
            if (user == null || user.Key == null)
            {
                Log.Warn("Feature flag evaluation called with null user or null user key. Returning default");
                sendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, null);
                return defaultValue;
            }

            try
            {
                var featureFlag = _featureStore.Get(VersionedDataKind.Features, featureKey);
                if (featureFlag == null)
                {
                    Log.InfoFormat("Unknown feature flag {0}; returning default value",
                        featureKey);

                    sendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, null);
                    return defaultValue;
                }

                FeatureFlag.EvalResult evalResult = featureFlag.Evaluate(user, _featureStore, _configuration);
                if (!IsOffline())
                {
                    foreach (var prereqEvent in evalResult.PrerequisiteEvents)
                    {
                        _eventStore.Add(prereqEvent);
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

                        sendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, featureFlag.Version);
                        return defaultValue;
                    }
                    sendFlagRequestEvent(featureKey, user, evalResult.Result, defaultValue, featureFlag.Version);
                    return evalResult.Result;
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
            sendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, null);
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
            _eventStore.Add(new CustomEvent(name, EventUser.FromUser(user, _configuration), data));
        }

        /// <see cref="ILdClient.Identify(User)"/>
        public void Identify(User user)
        {
            if (user == null || user.Key == null)
            {
                Log.Warn("Identify called with null user or null user key");
            }
            _eventStore.Add(new IdentifyEvent(EventUser.FromUser(user, _configuration)));
        }

        /// <see cref="ILdClient.Version"/>
        public Version Version
        {
            get
            {
                return typeof(LdClient).GetTypeInfo().Assembly.GetName().Version;
            }
        }

        private void sendFlagRequestEvent(string key, User user, JToken value, JToken defaultValue, JToken version)
        {
            _eventStore.Add(new FeatureRequestEvent(key, EventUser.FromUser(user, _configuration), value, defaultValue, version, null));
        }

        /// <summary>
        /// Used internally.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            Log.Info("Closing LaunchDarkly client.");
            //We do not have native resource, so the boolean parameter can be ignored.
            if (_eventStore is EventProcessor)
                ((_eventStore) as IDisposable).Dispose();

            if (_updateProcessor != null)
            {
                _updateProcessor.Dispose();
            }
        }

        /// <see cref="ILdClient.Dispose"/>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <see cref="ILdClient.Flush"/>
        public void Flush()
        {
            _eventStore.Flush();
        }
    }
}