using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal.Concurrent;
using LaunchDarkly.Sdk.Json;
using LaunchDarkly.TestHelpers.HttpTest;

namespace TestService
{
    public class CallbackService
    {
        // This class has both synchronous and asynchronous methods because in some cases the
        // test fixture method making the call is implementing a synchronous SDK interface method.

        private static readonly HttpClient _httpClient = new HttpClient();

        private readonly Uri _uri;
        
        public CallbackService(Uri uri)
        {
            _uri = uri;
        }

        public void Close() =>
            AsyncUtils.WaitSafely(() => _httpClient.DeleteAsync(_uri));

        public void Post(string path, object parameters) =>
            AsyncUtils.WaitSafely(() => PostAsync(path, parameters));

        public T Post<T>(string path, object parameters) =>
            AsyncUtils.WaitSafely(() => PostAsync<T>(path, parameters));

        public async Task PostAsync(string path, object parameters)
        {
            var resp = await PostInternalAsync(path, parameters);
            resp.Dispose();
        }

        public async Task<T> PostAsync<T>(string path, object parameters)
        {
            var resp = await PostInternalAsync(path, parameters);
            var body = await resp.Content.ReadAsStringAsync();
            var ret = JsonSerializer.Deserialize<T>(body, SimpleJsonService.SerializerOptions);
            // SimpleJsonService.SerializerOptions provides the default property name camelcasing behavior
            resp.Dispose();
            return ret;
        }

        private async Task<HttpResponseMessage> PostInternalAsync(string path, object parameters)
        {
            var resp = await _httpClient.PostAsync(_uri + path,
                new StringContent(
                    parameters == null ? "{}" : JsonSerializer.Serialize(parameters, SimpleJsonService.SerializerOptions),
                    Encoding.UTF8,
                    "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                string body = resp.Content == null ? "" : await resp.Content.ReadAsStringAsync();
                throw new Exception(string.Format("Callback to {0} returned HTTP error {1} {2}",
                    _uri + path, (int)resp.StatusCode, body));
            }
            return resp;
        }
    }
}
