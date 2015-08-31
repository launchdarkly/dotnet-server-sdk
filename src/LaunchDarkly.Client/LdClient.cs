using System;
using System.IO;
using System.Net;
using LaunchDarkly.Client.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    public class LdClient : IDisposable
    {
        private static ILog Logger = LogProvider.For<LdClient>();

        private readonly HttpClient _httpClient;
        private readonly Configuration _configuration;
        private readonly IStoreEvents _eventStore;

        public LdClient(Configuration config, HttpClient client, IStoreEvents eventStore)
        {
            var version = System.Reflection.Assembly.GetAssembly(typeof(LdClient)).GetName().Version;
            client.BaseAddress = config.BaseUri;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("api_key", config.ApiKey);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetClient/" + version);
            _configuration = config;
            _eventStore = eventStore;
            _httpClient = client;
        }

        public LdClient(Configuration config) : this(config, new HttpClient(new WebRequestHandler() {CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Default) }), new EventProcessor(config))
        {
        }

        public LdClient(String apiKey) : this(Configuration.Default().WithApiKey(apiKey))
        {

        }

        public async Task<bool> Toggle(string key, User user, bool defaultValue = false)
        {
            try
            {

                using (var response = await _httpClient.GetAsync(string.Format("api/eval/features/{0}", key)).ConfigureAwait(false))
                {
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
                            Logger.Error("Unexpected status code: " + response.ReasonPhrase);
                        }
                        sendFlagRequestEvent(key, user, defaultValue, true);
                        return defaultValue;
                    }

                    var feature = await response.Content.ReadAsAsync<Feature>().ConfigureAwait(false);
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

        public void Track(string name, User user, string data)
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

            _httpClient.Dispose();
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
