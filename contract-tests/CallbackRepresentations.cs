using System.Collections.Generic;
using System.Collections.Immutable;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Hooks;

namespace TestService
{
    public class BigSegmentStoreGetMetadataResponse
    {
        public long? LastUpToDate { get; set; }
    }

    public class BigSegmentStoreGetMembershipParams
    {
        public string ContextHash { get; set; }
    }

    public class BigSegmentStoreGetMembershipResponse
    {
        public Dictionary<string, bool?> Values { get; set; }
    }

    public class EvaluationHookParams
    {
        public EvaluationSeriesContext EvaluationSeriesContext { get; set; }
        public ImmutableDictionary<string, object> EvaluationSeriesData { get; set; }
        public EvaluateFlagResponse EvaluationDetail { get; set; }
        public string Stage { get; set; }
    }
}
