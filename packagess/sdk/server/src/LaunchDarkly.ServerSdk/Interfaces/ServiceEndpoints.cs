using System;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Specifies the base service URIs used by SDK components.
    /// </summary>
    /// <remarks>
    /// This class's properties are not public, since they are only read by the SDK.
    /// </remarks>
    /// <seealso cref="LaunchDarkly.Sdk.Server.Integrations.ServiceEndpointsBuilder"/>
    public sealed class ServiceEndpoints
    {
        internal Uri StreamingBaseUri { get; }
        internal Uri PollingBaseUri { get; }
        internal Uri EventsBaseUri { get; }

        internal ServiceEndpoints(
            Uri streamingBaseUri,
            Uri pollingBaseUri,
            Uri eventsBaseUri
            )
        {
            StreamingBaseUri = streamingBaseUri;
            PollingBaseUri = pollingBaseUri;
            EventsBaseUri = eventsBaseUri;
        }
    }
}
