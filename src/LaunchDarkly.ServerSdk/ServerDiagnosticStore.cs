using System;
using System.Collections.Generic;
using System.Threading;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{

    internal class ServerDiagnosticStore : IDiagnosticStore
    {
        private readonly Configuration Config;
        private readonly Dictionary<string, object> InitEvent;
        private readonly DiagnosticId DiagnosticId;

        // DataSince is stored in the "binary" long format so Interlocked.Exchange can be used
        private long DataSince;
        private long DroppedEvents;
        private long DeduplicatedUsers;
        private readonly object StreamInitsLock = new object();
        private List<Dictionary<string, object>> StreamInits = new List<Dictionary<string, object>>();

        #region IDiagnosticStore interface properties
        IReadOnlyDictionary<string, object> IDiagnosticStore.InitEvent => InitEvent;
        IReadOnlyDictionary<string, object> IDiagnosticStore.PersistedUnsentEvent => null;
        DateTime IDiagnosticStore.DataSince => DateTime.FromBinary(Interlocked.Read(ref DataSince));
        #endregion

        internal ServerDiagnosticStore(Configuration config)
        {
            DateTime currentTime = DateTime.Now;
            Config = config;
            DataSince = currentTime.ToBinary();
            DiagnosticId = new DiagnosticId(config.SdkKey, Guid.NewGuid());
            InitEvent = BuildInitEvent(currentTime);
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
            configInfo["baseURI"] = Config.BaseUri.ToString();
            configInfo["eventsURI"] = Config.EventsUri.ToString();
            configInfo["streamURI"] = Config.StreamUri.ToString();
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
            Interlocked.Increment(ref DeduplicatedUsers);
        }

        public void IncrementDroppedEvents()
        {
            Interlocked.Increment(ref DroppedEvents);
        }

        public void AddStreamInit(DateTime timestamp, TimeSpan duration, bool failed)
        {
            Dictionary<string, object> streamInitObject = new Dictionary<string, object>();
            streamInitObject.Add("timestamp", Util.GetUnixTimestampMillis(timestamp));
            streamInitObject.Add("durationMillis", duration.TotalMilliseconds);
            streamInitObject.Add("failed", failed);
            lock (StreamInitsLock)
            {
                StreamInits.Add(streamInitObject);
            }
        }

        public IReadOnlyDictionary<string, object> CreateEventAndReset(long eventsInQueue)
        {
            DateTime currentTime = DateTime.Now;
            long droppedEvents = Interlocked.Exchange(ref DroppedEvents, 0);
            long deduplicatedUsers = Interlocked.Exchange(ref DeduplicatedUsers, 0);
            long dataSince = Interlocked.Exchange(ref DataSince, currentTime.ToBinary());

            Dictionary<string, object> statEvent = new Dictionary<string, object>();
            AddDiagnosticCommonFields(statEvent, "diagnostic", currentTime);
            statEvent["eventsInQueue"] = eventsInQueue;
            statEvent["dataSinceDate"] = Util.GetUnixTimestampMillis(DateTime.FromBinary(dataSince));
            statEvent["droppedEvents"] = droppedEvents;
            statEvent["deduplicatedUsers"] = deduplicatedUsers;
            lock (StreamInitsLock) {
                statEvent["streamInits"] = StreamInits;
                StreamInits = new List<Dictionary<string, object>>();
            }

            return statEvent;
        }

        #endregion
    }
}
