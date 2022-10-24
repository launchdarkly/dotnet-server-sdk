using System;
using LaunchDarkly.Sdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TestService
{
    public class Status
    {
        public string Name { get; set; }
        public string[] Capabilities { get; set; }
        public string ClientVersion { get; set; }
    }

    public class CreateInstanceParams
    {
        public SdkConfigParams Configuration { get; set; }
        public string Tag { get; set; }
    }

    public class SdkConfigParams
    {
        public string Credential { get; set; }
        public long? StartWaitTimeMs { get; set; }
        public bool InitCanFail { get; set; }
        public SdkConfigStreamParams Streaming { get; set; }
        public SdkConfigEventParams Events { get; set; }
        public SdkConfigBigSegmentsParams BigSegments { get; set; }
    }

    public class SdkConfigStreamParams
    {
        public Uri BaseUri { get; set; }
        public long? InitialRetryDelayMs { get; set; }
    }

    public class SdkConfigEventParams
    {
        public Uri BaseUri { get; set; }
        public bool AllAttributesPrivate { get; set; }
        public int? Capacity { get; set; }
        public bool EnableDiagnostics { get; set; }
        public string[] GlobalPrivateAttributes { get; set; }
        public long? FlushIntervalMs { get; set; }
        public bool InlineUsers { get; set; }
    }

    public class SdkConfigBigSegmentsParams
    {
        public Uri CallbackUri { get; set; }
        public long? StaleAfterMs { get; set; }
        public long? StatusPollIntervalMs { get; set; }
        public int? UserCacheSize { get; set; }
        public long? UserCacheTimeMs { get; set; }
    }

    public class CommandParams
    {
        public string Command { get; set; }
        public EvaluateFlagParams Evaluate { get; set; }
        public EvaluateAllFlagsParams EvaluateAll { get; set; }
        public IdentifyEventParams IdentifyEvent { get; set; }
        public CustomEventParams CustomEvent { get; set; }
        public AliasEventParams AliasEvent { get; set; }
    }

    public class EvaluateFlagParams
    {
        public string FlagKey { get; set; }
        public User User { get; set; }
        public String ValueType { get; set; }
        public LdValue Value { get; set; }
        public LdValue DefaultValue { get; set; }
        public bool Detail { get; set; }
    }

    public class EvaluateFlagResponse
    {
        public LdValue Value { get; set; }
        public int? VariationIndex { get; set; }
        public EvaluationReason? Reason { get; set; }
    }

    public class EvaluateAllFlagsParams
    {
        public User User { get; set; }
        public bool ClientSideOnly { get; set; }
        public bool DetailsOnlyForTrackedFlags { get; set; }
        public bool WithReasons { get; set; }
    }

    public class EvaluateAllFlagsResponse
    {
        public LdValue State { get; set; }
    }

    public class IdentifyEventParams
    {
        public User User { get; set; }
    }

    public class CustomEventParams
    {
        public string EventKey { get; set; }
        public User User { get; set; }
        public LdValue Data { get; set; }
        public bool OmitNullData { get; set; }
        public double? MetricValue { get; set; }
    }

    public class AliasEventParams
    {
        public User User { get; set; }
        public User PreviousUser { get; set; }
    }

    public class GetBigSegmentStoreStatusResponse
    {
        public bool Available { get; set; }
        public bool Stale { get; set; }
    }
}
