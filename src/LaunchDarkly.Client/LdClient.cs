using System;
using LaunchDarkly.Client.Logging;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    public class LdClient : IDisposable, ILdClient
    {
        private static ILog Logger = LogProvider.For<LdClient>();

        private readonly Configuration _configuration;
        private readonly IStoreEvents _eventStore;
        private readonly IFeatureStore _featureStore;
        private readonly FeatureRequestor _featureRequestor;
        private readonly IUpdateProcessor _updateProcessor;

        public LdClient(Configuration config, IStoreEvents eventStore)
        {
            Logger.Info("Starting LaunchDarkly Client..");
            _configuration = config;
            _eventStore = eventStore;
            _featureStore = new InMemoryFeatureStore();
            _featureRequestor = new FeatureRequestor(config);
            _updateProcessor = new PollingProcessor(config, _featureRequestor, _featureStore);
            var initTask = _updateProcessor.Start();
            Logger.Info("Waiting up to " + _configuration.StartWaitTime.TotalMilliseconds + " milliseconds for LaunchDarkly client to start..");
            var unused = initTask.Task.Wait(_configuration.StartWaitTime);
        }

        public LdClient(Configuration config) : this(config, new EventProcessor(config))
        {
        }

        public LdClient(string apiKey) : this(Configuration.Default().WithApiKey(apiKey))
        {
        }

        public bool Initialized()
        {
            return _updateProcessor.Initialized();
        }

        public bool Toggle(string key, User user, bool defaultValue = false)
        {
            try
            {
                var value = Evaluate(key, user, defaultValue);
                sendFlagRequestEvent(key, user, value, defaultValue);
                if (value.Type.Equals(JTokenType.Boolean))
                {
                    return value.Value<bool>();
                }

            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in LaunchDarkly client" + ex.Message);
            }
            sendFlagRequestEvent(key, user, defaultValue, defaultValue);
            return defaultValue;
        }

        public int IntVariation(string key, User user, int defaultValue)
        {
            try
            {
                var value = Evaluate(key, user, defaultValue);
                sendFlagRequestEvent(key, user, value, defaultValue);
                if (value.Type.Equals(JTokenType.Integer))
                {
                    return value.Value<int>();
                }

            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in LaunchDarkly client" + ex.Message);
            }
            sendFlagRequestEvent(key, user, defaultValue, defaultValue);
            return defaultValue;
        }


        public float FloatVariation(string key, User user, float defaultValue)
        {
            try
            {
                var value = Evaluate(key, user, defaultValue);
                sendFlagRequestEvent(key, user, value, defaultValue);
                if (value.Type.Equals(JTokenType.Float))
                {
                    return value.Value<float>();
                }

            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in LaunchDarkly client" + ex.Message);
            }
            sendFlagRequestEvent(key, user, defaultValue, defaultValue);
            return defaultValue;
        }


        public string StringVariation(string key, User user, string defaultValue)
        {
            try
            {
                var value = Evaluate(key, user, defaultValue);
                sendFlagRequestEvent(key, user, value, defaultValue);
                if (value.Type.Equals(JTokenType.String))
                {
                    return value.Value<string>();
                }

            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in LaunchDarkly client" + ex.Message);
            }
            sendFlagRequestEvent(key, user, defaultValue, defaultValue);
            return defaultValue;
        }


        public JToken JsonVariation(string key, User user, JToken defaultValue)
        {
            try
            {
                var value = Evaluate(key, user, defaultValue);
                sendFlagRequestEvent(key, user, value, defaultValue);
                return value;

            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in LaunchDarkly client" + ex.Message);
            }
            sendFlagRequestEvent(key, user, defaultValue, defaultValue);
            return defaultValue;
        }

        private JToken Evaluate(string featureKey, User user, JToken defaultValue)
        {
            if (!Initialized())
            {
                Logger.Warn("LaunchDarkly client was not initialized. Returning default value. See previous log statements for more info");
                return defaultValue;
            }
            try
            {
                var featureFlag = _featureStore.Get(featureKey);
                if (featureFlag == null)
                {
                    Logger.Warn("Unknown feature flag " + featureKey + "; returning default value: ");
                    return defaultValue;
                }

                if (featureFlag.On)
                {
                    var evalResult = featureFlag.Evaluate(user, _featureStore);
                    if (evalResult.HasValue)
                    {
                        foreach (var prereqEvent in evalResult.Value.PrerequisiteEvents)
                        {
                            _eventStore.Add(prereqEvent);

                        }
                        return evalResult.Value.Result ?? defaultValue;
                    }
                }
                else
                {
                    var offVariation = featureFlag.OffVariationValue;
                    if (offVariation != null)
                    {
                        return offVariation;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    String.Format(
                        "Encountered exception in LaunchDarkly client: {0} when evaluating feature key: {1} for user key: {2}",
                        e.Message, featureKey, user.Key));
                Logger.Debug(e.ToString());
            }
            return defaultValue;
        }

        public void Track(string name, User user, string data)
        {
            _eventStore.Add(new CustomEvent(name, user, data));
        }

        public void Identify(User user)
        {
            _eventStore.Add(new IdentifyEvent(user));
        }

        private void sendFlagRequestEvent(string key, User user, JToken value, JToken defaultValue)
        {
            _eventStore.Add(new FeatureRequestEvent(key, user, value, defaultValue));
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Info("Closing LaunchDarkly client.");
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
