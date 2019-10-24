using System;
using System.Collections.Generic;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{

    internal class ServerDiagnosticStore : IDiagnosticStore
    {
        private readonly Configuration Config;
        private readonly Dictionary<string, object> InitEvent;
        private readonly DiagnosticId DiagnosticId;

        private readonly object UpdateLock = new object();
        private DateTime DataSince;
        private long DroppedEvents;
        private long DeduplicatedUsers;
        private List<Dictionary<string, object>> StreamInits = new List<Dictionary<string, object>>();

        #region IDiagnosticStore interface properties
        IReadOnlyDictionary<string, object> IDiagnosticStore.InitEvent => InitEvent;
        IReadOnlyDictionary<string, object> IDiagnosticStore.LastStats => null;
        DateTime IDiagnosticStore.DataSince => DataSince;
        #endregion

        internal ServerDiagnosticStore(Configuration config)
        {
            Config = config;
            DataSince = DateTime.Now;
            DiagnosticId = new DiagnosticId(config.SdkKey, Guid.NewGuid());
            InitEvent = BuildInitEvent(DataSince);
        }

        private void AddDiagnosticCommonFields(Dictionary<string, object> fieldDictionary, string kind, DateTime creationDate)
        {
            fieldDictionary.Add("kind", kind);
            fieldDictionary.Add("id", DiagnosticId);
            fieldDictionary.Add("creationDate", Util.GetUnixTimestampMillis(creationDate));
        }

        #region Init event builders

        private Dictionary<string, object> BuildInitEvent(DateTime creationDate)
        {
            Dictionary<string, object> InitEvent = new Dictionary<string, object>();
            InitEvent["configuration"] = InitEventConfig();
            InitEvent["sdk"] = InitEventSdk();
            InitEvent["platform"] = InitEventPlatform();
            AddDiagnosticCommonFields(InitEvent, "diagnostic-init", creationDate);
            return InitEvent;
        }

        private Dictionary<string, object> InitEventPlatform()
        {
            Dictionary<string, object> PlatformInfo = new Dictionary<string, object>();
            PlatformInfo["name"] = "dotnet";
            return PlatformInfo;
        }

        private Dictionary<string, object> InitEventSdk()
        {
            Dictionary<string, object> SdkInfo = new Dictionary<string, object>();
            SdkInfo["name"] = "dotnet-server-sdk";
            SdkInfo["version"] = ServerSideClientEnvironment.Instance.Version.ToString();
            SdkInfo["wrapperName"] = Config.WrapperName;
            SdkInfo["wrapperVersion"] = Config.WrapperVersion;
            return SdkInfo;
        }

        private Dictionary<string, object> InitEventConfig()
        {
            Dictionary<string, object> ConfigInfo = new Dictionary<string, object>();
            ConfigInfo["baseURI"] = Config.BaseUri;
            ConfigInfo["eventsURI"] = Config.EventsUri;
            ConfigInfo["streamURI"] = Config.StreamUri;
            ConfigInfo["eventsCapacity"] = Config.EventCapacity;
            ConfigInfo["connectTimeoutMillis"] = (long)Config.HttpClientTimeout.TotalMilliseconds;
            ConfigInfo["socketTimeoutMillis"] = (long)Config.ReadTimeout.TotalMilliseconds;
            ConfigInfo["eventsFlushIntervalMillis"] = (long)Config.EventFlushInterval.TotalMilliseconds;
            ConfigInfo["usingProxy"] = false;
            ConfigInfo["usingProxyAuthenticator"] = false;
            ConfigInfo["streamingDisabled"] = !Config.IsStreamingEnabled;
            ConfigInfo["usingRelayDaemon"] = Config.UseLdd;
            ConfigInfo["offline"] = Config.Offline;
            ConfigInfo["allAttributesPrivate"] = Config.AllAttributesPrivate;
            ConfigInfo["eventReportingDisabled"] = false;
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
            if (Config.FeatureStoreFactory != null)
            {
                ConfigInfo["featureStoreFactory"] = Config.FeatureStoreFactory.GetType().Name;
            }
            return ConfigInfo;
        }

        #endregion

        #region Periodic event update and builder methods

        public void IncrementDeduplicatedUsers()
        {
            lock (UpdateLock)
            {
                this.DeduplicatedUsers++;
            }
        }

        public void IncrementDroppedEvents()
        {
            lock (UpdateLock)
            {
                this.DroppedEvents++;
            }
        }

        public void AddStreamInit(long timestamp, int durationMillis, bool failed)
        {
            Dictionary<string, object> StreamInitObject = new Dictionary<string, object>();
            StreamInitObject.Add("timestamp", timestamp);
            StreamInitObject.Add("durationMillis", durationMillis);
            StreamInitObject.Add("failed", failed);
            lock (UpdateLock)
            {
                StreamInits.Add(StreamInitObject);
            }
        }

        public IReadOnlyDictionary<string, object> CreateEventAndReset(long eventsInQueue)
        {
            DateTime CurrentTime = DateTime.Now;
            Dictionary<string, object> StatEvent = new Dictionary<string, object>();
            AddDiagnosticCommonFields(StatEvent, "diagnostic", CurrentTime);
            StatEvent["eventsInQueue"] = eventsInQueue;
            lock (UpdateLock) {
                StatEvent["dataSinceDate"] = Util.GetUnixTimestampMillis(DataSince);
                StatEvent["droppedEvents"] = DroppedEvents;
                StatEvent["deduplicatedUsers"] = DeduplicatedUsers;
                StatEvent["streamInits"] = StreamInits;

                DataSince = CurrentTime;
                DroppedEvents = 0;
                DeduplicatedUsers = 0;
                StreamInits = new List<Dictionary<string, object>>();
            }

            return StatEvent;
        }

        #endregion
    }
}
