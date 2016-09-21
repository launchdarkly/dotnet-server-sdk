using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    public class LdClient : IDisposable, ILdClient
    {
        private ILogger Logger  = LdLogger.CreateLogger<LdClient>();

        private readonly Configuration _configuration;
        private readonly IStoreEvents _eventStore;
        private readonly IFeatureStore _featureStore;
        private readonly FeatureRequestor _featureRequestor;
        private readonly IUpdateProcessor _updateProcessor;

        public LdClient(Configuration config, IStoreEvents eventStore)
        {
            Logger.LogInformation("Starting LaunchDarkly Client..");
            _configuration = config;
            _eventStore = eventStore;
            _featureStore = new InMemoryFeatureStore();

          if (_configuration.Offline)
          {
              Logger.LogInformation("Starting Launchdarkly client in offline mode.");
              return;
          }

            _featureRequestor = new FeatureRequestor(config);
            _updateProcessor = new PollingProcessor(config, _featureRequestor, _featureStore);
            var initTask = _updateProcessor.Start();
            Logger.LogInformation("Waiting up to " + _configuration.StartWaitTime.TotalMilliseconds + " milliseconds for LaunchDarkly client to start..");
            var unused = initTask.Task.Wait(_configuration.StartWaitTime);
        }

        public LdClient(Configuration config) : this(config, new EventProcessor(config))
        {
        }

        public LdClient(string sdkKey) : this(Configuration.Default().WithSdkKey(sdkKey))
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
          Logger.LogWarning("Toggle() method is deprecated. Please use BoolVariation() instead");
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
          Logger.LogWarning("AllFlags() was called when client is in offline mode. Returning null.");
          return null;
        }
        if (!Initialized())
        {
          Logger.LogWarning("AllFlags() was called before client has finished initializing. Returning null.");
          return null;
        }
        if (user == null || user.Key == null)
        {
          Logger.LogWarning("AllFlags() called with null user or null user key. Returning null");
          return null;
        }

        IDictionary<string, FeatureFlag> flags = _featureStore.All();
        IDictionary<string, JToken> results = new Dictionary<string, JToken>();
        foreach (KeyValuePair<string, FeatureFlag> pair in flags)
        {
          try
          {
            FeatureFlag.EvalResult evalResult = pair.Value.Evaluate(user, _featureStore);
            results.Add(pair.Key, evalResult.Result);
          }
          catch (Exception e)
          {
            Logger.LogError("Exception caught when evaluating all flags: " + e.Message, e);
          }
        }
        return results;
      }

      private JToken Evaluate(string featureKey, User user, JToken defaultValue, JTokenType? expectedType)
        {
            if (!Initialized())
            {
                Logger.LogWarning("LaunchDarkly client has not yet been initialized. Returning default");
                return defaultValue;
            }
            if (user == null || user.Key == null)
            {
                Logger.LogWarning("Feature flag evaluation called with null user or null user key. Returning default");
                sendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, null);
                return defaultValue;
            }

            try
            {
                var featureFlag = _featureStore.Get(featureKey);
                if (featureFlag == null)
                {
                    Logger.LogWarning("Unknown feature flag " + featureKey + "; returning default value: ");
                    sendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, null);
                    return defaultValue;
                }

              FeatureFlag.EvalResult evalResult = featureFlag.Evaluate(user, _featureStore);
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
                  Logger.LogError("Expected type: " + expectedType + " but got " + evalResult.GetType() +
                               " when evaluating FeatureFlag: " + featureKey + ". Returning default");
                  sendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, featureFlag.Version);
                  return defaultValue;
                }
                sendFlagRequestEvent(featureKey, user, evalResult.Result, defaultValue, featureFlag.Version);
                return evalResult.Result;
              }
            }
            catch (Exception e)
            {
                Logger.LogError(
                    String.Format(
                        "Encountered exception in LaunchDarkly client: {0} when evaluating feature key: {1} for user key: {2}",
                        e.Message, featureKey, user.Key));
                Logger.LogDebug(e.ToString());
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
            Logger.LogWarning("Track called with null user or null user key");
          }
          _eventStore.Add(new CustomEvent(name, user, data));
        }

        public void Identify(User user)
        {
          if (user == null || user.Key == null)
          {
            Logger.LogWarning("Identify called with null user or null user key");
          }
          _eventStore.Add(new IdentifyEvent(user));
        }

        private void sendFlagRequestEvent(string key, User user, JToken value, JToken defaultValue, JToken version)
        {
            _eventStore.Add(new FeatureRequestEvent(key, user, value, defaultValue, version, null));
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.LogInformation("Closing LaunchDarkly client.");
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
