using System;
using LaunchDarkly.Client.Logging;

namespace LaunchDarkly.Client
{
    public class LdClient : IDisposable
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

        public LdClient(String apiKey) : this(Configuration.Default().WithApiKey(apiKey))
        {
        }

        public bool Initialized()
        {
            return _updateProcessor.Initialized();
        }

        public bool Toggle(string key, User user, bool defaultValue = false)
        {
            if (!_updateProcessor.Initialized())
            {
                return defaultValue;
            }

            try
            {
                bool value = evaluate(key, user, defaultValue);
                sendFlagRequestEvent(key, user, value, defaultValue);
                return value;
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in LaunchDarkly client" + ex.Message);
                sendFlagRequestEvent(key, user, defaultValue, defaultValue);
                return defaultValue;
            }
        }

        private bool evaluate(string key, User user, bool defaultValue)
        {
            Feature result = _featureStore.Get(key);
            if (result == null)
            {
                Logger.Warn("Unknown feature flag: " + key + "; returning default value: " + defaultValue);
                return defaultValue;
            }
            return result.Evaluate(user, defaultValue);
        }

        public void Track(string name, User user, string data)
        {
            _eventStore.Add(new CustomEvent(name, user, data));
        }

        public void Identify(User user)
        {
            _eventStore.Add(new IdentifyEvent(user));
        }

        private void sendFlagRequestEvent(string key, User user, Boolean value, Boolean usedDefaultValue)
        {
            _eventStore.Add(new FeatureRequestEvent<Boolean>(key, user, value, usedDefaultValue));
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
