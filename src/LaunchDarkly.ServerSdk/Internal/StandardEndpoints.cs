using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal
{
    internal static class StandardEndpoints
    {
        internal static readonly ServiceEndpoints BaseUris = new ServiceEndpoints(
            new Uri("https://stream.launchdarkly.com"),
            new Uri("https://sdk.launchdarkly.com"),
            new Uri("https://events.launchdarkly.com")
            );

        internal static Uri DefaultStreamingBaseUri = new Uri("https://stream.launchdarkly.com");
        internal static Uri DefaultPollingBaseUri = new Uri("https://sdk.launchdarkly.com");
        internal static Uri DefaultEventsBaseUri = new Uri("https://events.launchdarkly.com");

        internal const string StreamingRequestPath = "/all";

        internal const string PollingRequestPath = "/sdk/latest-all";

        internal const string AnalyticsEventsPostRequestPath = "/bulk";
        internal const string DiagnosticEventsPostRequestPath = "/diagnostic";

        internal static Uri SelectBaseUri(
            ServiceEndpoints configuredEndpoints,
            Func<ServiceEndpoints, Uri> uriGetter,
            string description,
            Logger errorLogger
            )
        {
            var configuredUri = uriGetter(configuredEndpoints);
            if (configuredUri != null)
            {
                return configuredUri;
            }
            errorLogger.Error(
                "You have set custom ServiceEndpoints without specifying the {0} base URI; connections may not work properly",
                description);
            return uriGetter(BaseUris);
        }

        internal static bool IsCustomUri(
            ServiceEndpoints configuredEndpoints,
            Uri overrideUri,
            Func<ServiceEndpoints, Uri> uriGetter
            ) =>
            !uriGetter(BaseUris).Equals(overrideUri ?? uriGetter(configuredEndpoints));
    }
}
