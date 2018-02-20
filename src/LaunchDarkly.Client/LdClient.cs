﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Common.Logging;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
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

        public LdClient(Configuration config) : this(config, new EventProcessor(config))
        {
        }

        public LdClient(string sdkKey) : this(Configuration.Default(sdkKey))
        {
        }

        public bool Initialized()
        {
            return IsOffline() || _updateProcessor.Initialized();
        }

        public bool IsOffline()
        {
            return _configuration.Offline;
        }


        [Obsolete("Please use BoolVariation instead.")]
        public bool Toggle(string key, User user, bool defaultValue = false)
        {
            Log.Warn("Toggle() method is deprecated. Please use BoolVariation() instead");
            return BoolVariation(key, user, defaultValue);
        }

        public bool BoolVariation(string key, User user, bool defaultValue = false)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.Boolean);
            return value.Value<bool>();
        }

        public int IntVariation(string key, User user, int defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.Integer);
            return value.Value<int>();
        }


        public float FloatVariation(string key, User user, float defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.Float);
            return value.Value<float>();
        }


        public string StringVariation(string key, User user, string defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, JTokenType.String);
            return value.Value<string>();
        }

        public JToken JsonVariation(string key, User user, JToken defaultValue)
        {
            var value = Evaluate(key, user, defaultValue, null);
            return value;
        }

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

        public void Track(string name, User user, string data)
        {
            if (user == null || user.Key == null)
            {
                Log.Warn("Track called with null user or null user key");
            }
            _eventStore.Add(new CustomEvent(name, EventUser.FromUser(user, _configuration), data));
        }

        public void Identify(User user)
        {
            if (user == null || user.Key == null)
            {
                Log.Warn("Identify called with null user or null user key");
            }
            _eventStore.Add(new IdentifyEvent(EventUser.FromUser(user, _configuration)));
        }

        private void sendFlagRequestEvent(string key, User user, JToken value, JToken defaultValue, JToken version)
        {
            _eventStore.Add(new FeatureRequestEvent(key, EventUser.FromUser(user, _configuration), value, defaultValue, version, null));
        }

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

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public void Flush()
        {
            _eventStore.Flush();
        }
    }
}