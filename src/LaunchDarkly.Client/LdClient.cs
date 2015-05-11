using System;
using System.IO;
using System.Net;
using LaunchDarkly.Client.Logging;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    public class LdClient : IDisposable
    {
        private static ILog Logger = LogProvider.For<LdClient>();

        private readonly Configuration _configuration;
        private readonly IStoreEvents _eventStore;

        public LdClient(Configuration config, IStoreEvents eventStore)
        {
            _configuration = config;
            _eventStore = eventStore;
        }

        public LdClient(Configuration config)
        {
            _configuration = config;
            _eventStore = new EventProcessor(_configuration);
        }

        HttpWebRequest CreateRequest(string key)
        {
            var url = new Uri(_configuration.BaseUri + string.Format("api/eval/features/{0}", key));
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers.Add(HttpRequestHeader.Authorization, "api_key " + _configuration.ApiKey);

            return request;
        }

        Feature GetFeature(HttpWebResponse response)
        {
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var json = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<Feature>(json);
            }
        }

        public bool GetFlag(string key, User user, bool defaultValue = false)
        {
            try
            {
                var request = CreateRequest(key);

                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    var feature = GetFeature(response);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            Logger.Error("Invalid API key");
                        }
                        else if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            Logger.Error("Unknown feature key: " + key);
                        }
                        else
                        {
                            Logger.Error("Unexpected status code: " + response.StatusDescription);
                        }
                        sendFlagRequestEvent(key, user, defaultValue, true);
                        return defaultValue;
                    }

                    var value = feature.Evaluate(user, defaultValue);
                    sendFlagRequestEvent(key, user, value, false);
                    return value;
                }
            }

            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in LaunchDarkly client" + ex.Message);
                sendFlagRequestEvent(key, user, defaultValue, true);
                return defaultValue;
            }
        }


        public void SendEvent(string name, User user, string data)
        {
            _eventStore.Add(new CustomEvent(name, user, data));
        }

        private void sendFlagRequestEvent(string key, User user, Boolean value, Boolean usedDefaultValue)
        {
            _eventStore.Add(new FeatureRequestEvent<Boolean>(key, user, value, usedDefaultValue));
        }


        protected virtual void Dispose(bool disposing)
        {
            //We do not have native resource, so the boolean parameter can be ignored.
            if (_eventStore is EventProcessor)
                ((_eventStore) as IDisposable).Dispose();
        }
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }
    }
}
