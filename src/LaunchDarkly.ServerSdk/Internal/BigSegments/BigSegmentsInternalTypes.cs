using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.BigSegments
{
    internal static class BigSegmentsInternalTypes
    {
        // This type is used when Evaluator is querying big segments
        internal struct BigSegmentsQueryResult
        {
            internal IMembership Membership { get; set; }
            internal BigSegmentsStatus Status { get; set; }
        }
    }
}
