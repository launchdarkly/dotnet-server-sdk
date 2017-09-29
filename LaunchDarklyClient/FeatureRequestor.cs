using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;

namespace LaunchDarklyClient
{
	internal class FeatureRequestor
	{
		private static readonly ILog log = LogManager.GetLogger<FeatureRequestor>();

		private readonly Configuration config;
		private readonly Uri uri;
		private volatile EntityTagHeaderValue etag;
		private volatile HttpClient httpClient;

		internal FeatureRequestor(Configuration config)
		{
			try
			{
				log.Trace($"Start constructor {nameof(FeatureRequestor)}(Configuration)");

				this.config = config;
				uri = new Uri(config.BaseUri.AbsoluteUri + "sdk/latest-flags");
				httpClient = config.HttpClient();
				
			}
			finally
			{
				log.Trace($"End constructor {nameof(FeatureRequestor)}(Configuration)");
			}
		}

		// Returns a dictionary of the latest flags, or null if they have not been modified. Throws an exception if there
		// was a problem getting flags.
		internal async Task<IDictionary<string, FeatureFlag>> MakeAllRequestAsync()
		{
			try
			{
				log.Trace($"Start {nameof(MakeAllRequestAsync)}");

				CancellationTokenSource cts = new CancellationTokenSource(config.HttpClientTimeout);
				try
				{
					return await FetchFeatureFlagsAsync(cts);
				}
				catch (Exception e)
				{
					// Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
					httpClient?.Dispose();
					httpClient = config.HttpClient();

					log.Debug($"Error getting feature flags: {Util.ExceptionMessage(e)} waiting 1 second before retrying.");
					Task.Delay(TimeSpan.FromSeconds(1)).Wait();
					cts = new CancellationTokenSource(config.HttpClientTimeout);

					try
					{
						return await FetchFeatureFlagsAsync(cts);
					}
					catch (TaskCanceledException tce)
					{
						// Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
						httpClient?.Dispose();
						httpClient = config.HttpClient();

						if (tce.CancellationToken == cts.Token)
						{
							//Indicates the task was cancelled by something other than a request timeout
							throw;
						}
						//Otherwise this was a request timeout.
						throw new Exception($"Get Features with URL: {uri.AbsoluteUri} timed out after : {config.HttpClientTimeout}", tce);
					}
					catch (Exception)
					{
						// Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
						httpClient?.Dispose();
						httpClient = config.HttpClient();
						throw;
					}
				}
			}
			finally
			{
				log.Trace($"End {nameof(MakeAllRequestAsync)}");
			}
		}

		private async Task<IDictionary<string, FeatureFlag>> FetchFeatureFlagsAsync(CancellationTokenSource cts)
		{
			try
			{
				log.Trace($"Start {nameof(FetchFeatureFlagsAsync)}");

				log.Debug($"Getting all flags with uri: {uri.AbsoluteUri}");
				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
				if (etag != null)
				{
					request.Headers.IfNoneMatch.Add(etag);
				}

				using (HttpResponseMessage response = await httpClient.SendAsync(request, cts.Token).ConfigureAwait(false))
				{
					if (response.StatusCode == HttpStatusCode.NotModified)
					{
						log.Debug("Get all flags returned 304: not modified");
						return null;
					}
					etag = response.Headers.ETag;
					//We ensure the status code after checking for 304, because 304 isn't considered success
					response.EnsureSuccessStatusCode();
					string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					IDictionary<string, FeatureFlag> flags = JsonConvert.DeserializeObject<IDictionary<string, FeatureFlag>>(content);
					log.Debug("Get all flags returned " + flags.Keys.Count + " feature flags");
					return flags;
				}
			}
			finally
			{
				log.Trace($"End {nameof(FetchFeatureFlagsAsync)}");
			}
		}
	}
}