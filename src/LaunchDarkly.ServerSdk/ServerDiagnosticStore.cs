using System;
using System.Collections.Generic;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client {

    internal class ServerDiagnosticStore : IDiagnosticStore {
        private DiagnosticId DiagnosticId;
        private DateTimeOffset DataSince;
        private long DroppedEvents;
        private long DeduplicatedUsers;
        private Dictionary<string, Object> InitEvent;
        private Configuration Config;

        // IDiagnosticStore interface properties
        Dictionary<string, Object> IDiagnosticStore.InitEvent => InitEvent;
        Dictionary<string, Object> IDiagnosticStore.LastStats => null;
        DateTimeOffset IDiagnosticStore.DataSince => DataSince;

        internal ServerDiagnosticStore(string sdkKey, Configuration config) {
            Config = config;
            DataSince = DateTimeOffset.Now;
            DiagnosticId = new DiagnosticId(sdkKey, Guid.NewGuid());
            InitEvent = BuildInitEvent(DataSince);
        }

        private void AddDiagnosticCommonFields(Dictionary<string, Object> fieldDictionary, string kind, DateTimeOffset creationDate) {
            fieldDictionary.Add("kind", kind);
            fieldDictionary.Add("id", DiagnosticId);
            fieldDictionary.Add("creationDate", creationDate.ToUnixTimeMilliseconds());
        }

        private Dictionary<string, Object> BuildInitEvent(DateTimeOffset creationDate) {
            InitEvent = new Dictionary<string, Object>();
            InitEvent["configuration"] = InitEventConfig();
            InitEvent["sdk"] = InitEventSdk();
            InitEvent["platform"] = InitEventPlatform();
            AddDiagnosticCommonFields(InitEvent, "diagnostic-init", creationDate);
            return InitEvent;
        }

        private Dictionary<string, Object> InitEventPlatform() {
            Dictionary<string, Object> PlatformInfo = new Dictionary<string, Object>();
            PlatformInfo["name"] = "dotnet";
            return PlatformInfo;
        }

        private Dictionary<string, Object> InitEventSdk()
        {
            Dictionary<string, Object> SdkInfo = new Dictionary<string, object>();
            SdkInfo["name"] = "dotnet-server-sdk";
            SdkInfo["version"] = ServerSideClientEnvironment.Instance.Version.ToString();
            SdkInfo["wrapperName"] = Config.WrapperName;
            SdkInfo["wrapperVersion"] = Config.WrapperVersion;
            return SdkInfo;
        }

        private Dictionary<string, Object> InitEventConfig()
        {
            Dictionary<string, Object> ConfigInfo = new Dictionary<string, Object>();
            ConfigInfo["baseURI"] = Config.BaseUri;
            ConfigInfo["eventsURI"] = Config.EventsUri;
            ConfigInfo["streamURI"] = Config.StreamUri;
            ConfigInfo["eventsCapacity"] = Config.EventCapacity;
            //ConfigInfo["connectTimeoutMillis"] = Config.ConnectTimeoutMillis;
            //ConfigInfo["socketTimeoutMillis"] = Config.SocketTimeoutMillis;
            ConfigInfo["eventsFlushIntervalMillis"] = (long)Config.EventFlushInterval.TotalMilliseconds;
            ConfigInfo["usingProxy"] = false;
            ConfigInfo["usingProxyAuthenticator"] = false;
            ConfigInfo["streamingDisabled"] = !Config.IsStreamingEnabled;
            ConfigInfo["usingRelayDaemon"] = Config.UseLdd;
            ConfigInfo["offline"] = Config.Offline;
            ConfigInfo["allAttributesPrivate"] = Config.AllAttributesPrivate;
            //ConfigInfo["eventReportingDisabled"] = Config.EventReportingDisabled;
            ConfigInfo["pollingIntervalMillis"] = (long)Config.PollingInterval.TotalMilliseconds;
            ConfigInfo["startWaitMillis"] = (long)Config.StartWaitTime.TotalMilliseconds;
#pragma warning disable 618
            ConfigInfo["samplingInterval"] = Config.EventSamplingInterval;
#pragma warning restore 618
            ConfigInfo["reconnectTimeMillis"] = (long)Config.ReconnectTime.TotalMilliseconds;
            ConfigInfo["userKeysCapacity"] = Config.UserKeysCapacity;
            ConfigInfo["userKeysFlushIntervalMillis"] = (long)Config.UserKeysFlushInterval.TotalMilliseconds;
            ConfigInfo["inlineUsersInEvents"] = Config.InlineUsersInEvents;
            ConfigInfo["diagnosticRecordingIntervalMillis"] = (long)Config.DiagnosticRecordingInterval.TotalMilliseconds;
            //ConfigInfo["featureStore"] = Config.FeatureStore.ToString;
            return ConfigInfo;
        }

        public void IncrementDeduplicatedUsers() {
            this.DeduplicatedUsers++;
        }

        public void IncrementDroppedEvents() {
            this.DroppedEvents++;
        }

        public Dictionary<string, Object> GetStatsAndReset(long eventsInQueue)
        {
            DateTimeOffset CurrentTime = DateTimeOffset.Now;
            Dictionary<string, Object> StatEvent = new Dictionary<string, Object>();
            StatEvent["dataSinceDate"] = DataSince.ToUnixTimeMilliseconds();
            StatEvent["droppedEvents"] = DroppedEvents;
            StatEvent["deduplicatedUsers"] = DeduplicatedUsers;
            StatEvent["eventsInQueue"] = eventsInQueue;
            AddDiagnosticCommonFields(StatEvent, "diagnostic", CurrentTime);

            DataSince = CurrentTime;
            DroppedEvents = 0;
            DeduplicatedUsers = 0;

            return StatEvent;
        }
    }
}
