using System;
using LaunchDarkly.Sdk.Server.Integrations;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Encapsulates the SDK's configuration with regard to Big Segments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Big Segments" are a specific type of user segments. For more information, read the LaunchDarkly
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
        /// The value set by <see cref="BigSegmentsConfigurationBuilder.UserCacheSize(int)"/>.
        /// </summary>
        public int UserCacheSize { get; }

        /// <summary>
        /// The value set by <see cref="BigSegmentsConfigurationBuilder.UserCacheTime(TimeSpan)"/>.
        /// </summary>
        public TimeSpan UserCacheTime { get; }

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
        /// <param name="userCacheSize">value for UserCacheSize</param>
        /// <param name="userCacheTime">value for UserCacheTime</param>
        /// <param name="statusPollInterval">value for StatusPollInterval</param>
        /// <param name="staleAfter">value for StaleAfter</param>
        public BigSegmentsConfiguration(
            IBigSegmentStore store,
            int userCacheSize,
            TimeSpan userCacheTime,
            TimeSpan statusPollInterval,
            TimeSpan staleAfter
            )
        {
            Store = store;
            UserCacheSize = userCacheSize;
            UserCacheTime = userCacheTime;
            StatusPollInterval = statusPollInterval;
            StaleAfter = staleAfter;
        }
    }
}
