using System;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Information about the status of a Big Segment store, provided by
    /// <see cref="IBigSegmentStoreStatusProvider"/>.
    /// </summary>
    /// <remarks>
    /// "Big Segments" are a specific type of user segments. For more information, read the LaunchDarkly
    /// documentation about user segments: https://docs.launchdarkly.com/home/users/segments
    /// </remarks>
    public struct BigSegmentStoreStatus
    {
        /// <summary>
        /// True if the Big Segment store is able to respond to queries, so that the SDK can
        /// evaluate whether a user is in a segment or not.
        /// </summary>
        /// <remarks>
        /// If this property is false, the store is not able to make queries (for instance, it may not have
        /// a valid database connection). In this case, the SDK will treat any reference to a Big Segment
        /// as if no users are included in that segment. Also, the <see cref="EvaluationReason"/>
        /// associated with any flag evaluation that references a Big Segment when the store is not
        /// available will have a <see cref="EvaluationReason.BigSegmentsStatus"/> of
        /// <see cref="BigSegmentsStatus.StoreError"/>.
        /// </remarks>
        public bool Available { get; set; }

        /// <summary>
        /// True if the Big Segment store is available, but has not been updated within the amount of time
        /// specified by <see cref="LaunchDarkly.Sdk.Server.Integrations.BigSegmentsConfigurationBuilder.StaleAfter(TimeSpan)"/>.
        /// </summary>
        /// <remarks>
        /// This may indicate that the LaunchDarkly Relay Proxy, which populates the store, has stopped
        /// running or has become unable to receive fresh data from LaunchDarkly. Any feature flag
        /// evaluations that reference a Big Segment will be using the last known data, which may be out
        /// of date.
        /// </remarks>
        public bool Stale { get; set; }

        /// <inheritdoc/>
        public override string ToString() =>
            string.Format("(Available={0},Stale={1})", Available, Stale);
    }
}
