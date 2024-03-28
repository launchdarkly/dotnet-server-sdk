using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk;

// Note, in order for System.Text.Json serialization/deserialization to work correctly, the members of
// this class must be properties with get/set, rather than fields. The property names are automatically
// camelCased by System.Text.Json.

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
        public SdkTagParams Tags { get; set; }
        public SdkHookParams Hooks { get; set; }
    }

    public class SdkTagParams
    {
        public string ApplicationId { get; set; }
        public string ApplicationVersion { get; set; }
    }

    public class HookData
    {
        public Dictionary<string, LdValue> BeforeEvaluation { get; set; }
        public Dictionary<string, LdValue> AfterEvaluation { get; set; }
    }
    public class HookConfig
    {
        public string Name { get; set; }
        public Uri CallbackUri { get; set; }
        public HookData Data { get; set; }
    }

    public class SdkHookParams
    {
        public List<HookConfig> Hooks { get; set; }
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
        public ContextBuildParams ContextBuild { get; set; }
        public ContextConvertParams ContextConvert { get; set; }
        public SecureModeHashParams SecureModeHash { get; set; }
        public MigrationVariationParams MigrationVariation { get; set; }
        public MigrationOperationParams MigrationOperation { get; set; }
    }

    public class EvaluateFlagParams
    {
        public string FlagKey { get; set; }
        public Context? Context { get; set; }
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
        public Context? Context { get; set; }
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
        public Context? Context { get; set; }
        public User User { get; set; }
    }

    public class CustomEventParams
    {
        public string EventKey { get; set; }
        public Context? Context { get; set; }
        public User User { get; set; }
        public LdValue Data { get; set; }
        public bool OmitNullData { get; set; }
        public double? MetricValue { get; set; }
    }

    public class GetBigSegmentStoreStatusResponse
    {
        public bool Available { get; set; }
        public bool Stale { get; set; }
    }

    public class ContextBuildParams
    {
        public ContextBuildSingleParams Single { get; set; }
        public ContextBuildSingleParams[] Multi { get; set; }
    }

    public class ContextBuildSingleParams
    {
        public string Kind { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public bool Anonymous { get; set; }
        public string[] Private { get; set; }
        public Dictionary<string, LdValue> Custom { get; set; }
    }

    public class ContextBuildResponse
    {
        public string Output { get; set; }
        public string Error { get; set; }
    }

    public class ContextConvertParams
    {
        public string Input { get; set; }
    }

    public class SecureModeHashParams
    {
        public Context? Context { get; set; }
        public User User { get; set; }
    }

    public class SecureModeHashResponse
    {
        public string Result { get; set; }
    }

    public class MigrationVariationParams {
        public string Key { get; set; }
        public Context Context { get; set; }
        public string DefaultStage { get; set; }
    }

    public class MigrationVariationResponse {
        public string Result { get; set; }
    }

    public class MigrationOperationParams {
        public string Operation { get; set; }
        public Context Context { get; set; }
        public string Key { get; set; }
        public string DefaultStage { get; set; }
        public string Payload { get; set; }
        public string ReadExecutionOrder { get; set; }
        public bool TrackConsistency { get; set; }
        public bool TrackLatency { get; set; }
        public bool TrackErrors { get; set; }
        public Uri OldEndpoint { get; set; }
        public Uri NewEndpoint { get; set; }
    }

    public class MigrationOperationResponse {
        public string Result { get; set; }
        public string Error { get; set; }
    }
}
