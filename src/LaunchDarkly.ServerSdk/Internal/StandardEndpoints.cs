using System;

namespace LaunchDarkly.Sdk.Server.Internal
{
    internal static class StandardEndpoints
    {
        internal static Uri DefaultStreamingBaseUri = new Uri("https://stream.launchdarkly.com");
        internal static Uri DefaultPollingBaseUri = new Uri("https://sdk.launchdarkly.com");
        internal static Uri DefaultEventsBaseUri = new Uri("https://events.launchdarkly.com");

        internal const string StreamingRequestPath = "/all";

        internal const string PollingRequestPath = "/sdk/latest-all";

        internal const string AnalyticsEventsPostRequestPath = "/bulk";
        internal const string DiagnosticEventsPostRequestPath = "/diagnostic";
    }
}
