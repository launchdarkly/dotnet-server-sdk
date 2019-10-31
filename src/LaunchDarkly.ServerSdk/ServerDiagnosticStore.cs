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
        IReadOnlyDictionary<string, object> IDiagnosticStore.PersistedUnsentEvent => null;
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
            Dictionary<string, object> initEvent = new Dictionary<string, object>();
            initEvent["configuration"] = InitEventConfig();
            initEvent["sdk"] = InitEventSdk();
            initEvent["platform"] = InitEventPlatform();
            AddDiagnosticCommonFields(initEvent, "diagnostic-init", creationDate);
            return initEvent;
        }

        private Dictionary<string, object> InitEventPlatform()
        {
            Dictionary<string, object> platformInfo = new Dictionary<string, object>();
            platformInfo["name"] = "dotnet";
            return platformInfo;
        }

        private Dictionary<string, object> InitEventSdk()
        {
            Dictionary<string, object> sdkInfo = new Dictionary<string, object>();
            sdkInfo["name"] = "dotnet-server-sdk";
            sdkInfo["version"] = ServerSideClientEnvironment.Instance.Version.ToString();
            sdkInfo["wrapperName"] = Config.WrapperName;
            sdkInfo["wrapperVersion"] = Config.WrapperVersion;
            return sdkInfo;
        }

        private Dictionary<string, object> InitEventConfig()
        {
            Dictionary<string, object> configInfo = new Dictionary<string, object>();
            configInfo["baseURI"] = Config.BaseUri;
            configInfo["eventsURI"] = Config.EventsUri;
            configInfo["streamURI"] = Config.StreamUri;
            configInfo["eventsCapacity"] = Config.EventCapacity;
            configInfo["connectTimeoutMillis"] = (long)Config.HttpClientTimeout.TotalMilliseconds;
            configInfo["socketTimeoutMillis"] = (long)Config.ReadTimeout.TotalMilliseconds;
            configInfo["eventsFlushIntervalMillis"] = (long)Config.EventFlushInterval.TotalMilliseconds;
            configInfo["usingProxy"] = false;
            configInfo["usingProxyAuthenticator"] = false;
            configInfo["streamingDisabled"] = !Config.IsStreamingEnabled;
            configInfo["usingRelayDaemon"] = Config.UseLdd;
            configInfo["offline"] = Config.Offline;
            configInfo["allAttributesPrivate"] = Config.AllAttributesPrivate;
            configInfo["eventReportingDisabled"] = false;
            configInfo["pollingIntervalMillis"] = (long)Config.PollingInterval.TotalMilliseconds;
            configInfo["startWaitMillis"] = (long)Config.StartWaitTime.TotalMilliseconds;
#pragma warning disable 618
            configInfo["samplingInterval"] = Config.EventSamplingInterval;
#pragma warning restore 618
            configInfo["reconnectTimeMillis"] = (long)Config.ReconnectTime.TotalMilliseconds;
            configInfo["userKeysCapacity"] = Config.UserKeysCapacity;
            configInfo["userKeysFlushIntervalMillis"] = (long)Config.UserKeysFlushInterval.TotalMilliseconds;
            configInfo["inlineUsersInEvents"] = Config.InlineUsersInEvents;
            configInfo["diagnosticRecordingIntervalMillis"] = (long)Config.DiagnosticRecordingInterval.TotalMilliseconds;
            if (Config.FeatureStoreFactory != null)
            {
                configInfo["featureStoreFactory"] = Config.FeatureStoreFactory.GetType().Name;
            }
            return configInfo;
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
            Dictionary<string, object> streamInitObject = new Dictionary<string, object>();
            streamInitObject.Add("timestamp", timestamp);
            streamInitObject.Add("durationMillis", durationMillis);
            streamInitObject.Add("failed", failed);
            lock (UpdateLock)
            {
                StreamInits.Add(streamInitObject);
            }
        }

        public IReadOnlyDictionary<string, object> CreateEventAndReset(long eventsInQueue)
        {
            DateTime currentTime = DateTime.Now;
            Dictionary<string, object> statEvent = new Dictionary<string, object>();
            AddDiagnosticCommonFields(statEvent, "diagnostic", currentTime);
            statEvent["eventsInQueue"] = eventsInQueue;
            lock (UpdateLock) {
                statEvent["dataSinceDate"] = Util.GetUnixTimestampMillis(DataSince);
                statEvent["droppedEvents"] = DroppedEvents;
                statEvent["deduplicatedUsers"] = DeduplicatedUsers;
                statEvent["streamInits"] = StreamInits;

                DataSince = currentTime;
                DroppedEvents = 0;
                DeduplicatedUsers = 0;
                StreamInits = new List<Dictionary<string, object>>();
            }

            return statEvent;
        }

        #endregion
    }
}
