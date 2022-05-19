using System;
using LaunchDarkly.Sdk.Server.Integrations;

namespace LaunchDarkly.Sdk.Server.Subsystems
{
    /// <summary>
    /// Encapsulates the SDK's configuration with regard to Big Segments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Big Segments" are a specific type of segments. For more information, read the LaunchDarkly
    /// documentation about user segments: https://docs.launchdarkly.com/home/users/segments
    /// </para>
    /// <para>
    /// See <see cref="BigSegmentsConfigurationBuilder"/> for more details on these properties.
    /// </para>
    /// </remarks>
    public sealed class BigSegmentsConfiguration
    {
        /// <summary>
        /// The data store instance that is used for Big Segments data.
        /// </summary>
        public IBigSegmentStore Store { get; }

        /// <summary>
        /// The value set by <see cref="BigSegmentsConfigurationBuilder.ContextCacheSize(int)"/>.
        /// </summary>
        public int ContextCacheSize { get; }

        /// <summary>
        /// The value set by <see cref="BigSegmentsConfigurationBuilder.ContextCacheTime(TimeSpan)"/>.
        /// </summary>
        public TimeSpan ContextCacheTime { get; }

        /// <summary>
        /// The value set by <see cref="BigSegmentsConfigurationBuilder.StatusPollInterval(TimeSpan)"/>.
        /// </summary>
        public TimeSpan StatusPollInterval { get; }

        /// <summary>
        /// The value set by <see cref="BigSegmentsConfigurationBuilder.StaleAfter(TimeSpan)"/>.
        /// </summary>
        public TimeSpan StaleAfter { get; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="store">value for Store</param>
        /// <param name="contextCacheSize">value for ContextCacheSize</param>
        /// <param name="contextCacheTime">value for ContextCacheTime</param>
        /// <param name="statusPollInterval">value for StatusPollInterval</param>
        /// <param name="staleAfter">value for StaleAfter</param>
        public BigSegmentsConfiguration(
            IBigSegmentStore store,
            int contextCacheSize,
            TimeSpan contextCacheTime,
            TimeSpan statusPollInterval,
            TimeSpan staleAfter
            )
        {
            Store = store;
            ContextCacheSize = contextCacheSize;
            ContextCacheTime = contextCacheTime;
            StatusPollInterval = statusPollInterval;
            StaleAfter = staleAfter;
        }
    }
}
