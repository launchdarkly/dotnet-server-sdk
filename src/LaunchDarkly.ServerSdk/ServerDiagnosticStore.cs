using System;
using System.Collections.Generic;
using System.Threading;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{

    internal class ServerDiagnosticStore : IDiagnosticStore
    {
        private readonly Configuration Config;
        private readonly DiagnosticEvent InitEvent;
        private readonly DiagnosticId DiagnosticId;

        // DataSince is stored in the "binary" long format so Interlocked.Exchange can be used
        private long DataSince;
        private long DroppedEvents;
        private long DeduplicatedUsers;
        private readonly object StreamInitsLock = new object();
        private LdValue.ArrayBuilder StreamInits = LdValue.BuildArray();

        #region IDiagnosticStore interface properties
        DiagnosticEvent? IDiagnosticStore.InitEvent => InitEvent;
        DiagnosticEvent? IDiagnosticStore.PersistedUnsentEvent => null;
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

        private void AddDiagnosticCommonFields(LdValue.ObjectBuilder fieldsBuilder, string kind, DateTime creationDate)
        {
            fieldsBuilder.Add("kind", kind);
            fieldsBuilder.Add("id", EncodeDiagnosticId(DiagnosticId));
            fieldsBuilder.Add("creationDate", Util.GetUnixTimestampMillis(creationDate));
        }

        private LdValue EncodeDiagnosticId(DiagnosticId id)
        {
            var o = LdValue.BuildObject().Add("diagnosticId", id._diagnosticId.ToString());
            if (id._sdkKeySuffix != null)
            {
                o.Add("sdkKeySuffix", id._sdkKeySuffix);
            }
            return o.Build();
        }

        #region Init event builders

        private DiagnosticEvent BuildInitEvent(DateTime creationDate)
        {
            var initEvent = LdValue.BuildObject();
            initEvent.Add("configuration", InitEventConfig());
            initEvent.Add("sdk", InitEventSdk());
            initEvent.Add("platform", InitEventPlatform());
            AddDiagnosticCommonFields(initEvent, "diagnostic-init", creationDate);
            return new DiagnosticEvent(initEvent.Build());
        }

        private LdValue InitEventPlatform() =>
            LdValue.BuildObject().Add("name", "dotnet").Build();

        private LdValue InitEventSdk()
        {
            var sdkInfo = LdValue.BuildObject()
                .Add("name", "dotnet-server-sdk")
                .Add("version", ServerSideClientEnvironment.Instance.Version.ToString());
            if (Config.WrapperName != null)
            {
                sdkInfo.Add("wrapperName", Config.WrapperName);
            }
            if (Config.WrapperVersion != null)
            {
                sdkInfo.Add("wrapperVersion", Config.WrapperVersion);
            }
            return sdkInfo.Build();
        }

        private LdValue InitEventConfig()
        {
            var configInfo = LdValue.BuildObject();
            configInfo.Add("customBaseURI", !(Configuration.DefaultUri.Equals(Config.BaseUri)));
            configInfo.Add("customEventsURI", !(Configuration.DefaultEventsUri.Equals(Config.EventsUri)));
            configInfo.Add("customStreamURI", !(Configuration.DefaultStreamUri.Equals(Config.StreamUri)));
            configInfo.Add("eventsCapacity", Config.EventCapacity);
            configInfo.Add("connectTimeoutMillis", Config.HttpClientTimeout.TotalMilliseconds);
            configInfo.Add("socketTimeoutMillis", Config.ReadTimeout.TotalMilliseconds);
            configInfo.Add("eventsFlushIntervalMillis", Config.EventFlushInterval.TotalMilliseconds);
            configInfo.Add("usingProxy", false);
            configInfo.Add("usingProxyAuthenticator", false);
            configInfo.Add("streamingDisabled", !Config.IsStreamingEnabled);
            configInfo.Add("usingRelayDaemon", Config.UseLdd);
            configInfo.Add("offline", Config.Offline);
            configInfo.Add("allAttributesPrivate", Config.AllAttributesPrivate);
            configInfo.Add("eventReportingDisabled", false);
            configInfo.Add("pollingIntervalMillis", (long)Config.PollingInterval.TotalMilliseconds);
            configInfo.Add("startWaitMillis", (long)Config.StartWaitTime.TotalMilliseconds);
#pragma warning disable 618
            configInfo.Add("samplingInterval", Config.EventSamplingInterval);
#pragma warning restore 618
            configInfo.Add("reconnectTimeMillis", (long)Config.ReconnectTime.TotalMilliseconds);
            configInfo.Add("userKeysCapacity", Config.UserKeysCapacity);
            configInfo.Add("userKeysFlushIntervalMillis", (long)Config.UserKeysFlushInterval.TotalMilliseconds);
            configInfo.Add("inlineUsersInEvents", Config.InlineUsersInEvents);
            configInfo.Add("diagnosticRecordingIntervalMillis", (long)Config.DiagnosticRecordingInterval.TotalMilliseconds);
            if (Config.FeatureStoreFactory != null)
            {
                configInfo.Add("featureStoreFactory", Config.FeatureStoreFactory.GetType().Name);
            }
            return configInfo.Build();
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
            var streamInitObject = LdValue.BuildObject();
            streamInitObject.Add("timestamp", Util.GetUnixTimestampMillis(timestamp));
            streamInitObject.Add("durationMillis", duration.TotalMilliseconds);
            streamInitObject.Add("failed", failed);
            lock (StreamInitsLock)
            {
                StreamInits.Add(streamInitObject.Build());
            }
        }

        public DiagnosticEvent CreateEventAndReset(long eventsInQueue)
        {
            DateTime currentTime = DateTime.Now;
            long droppedEvents = Interlocked.Exchange(ref DroppedEvents, 0);
            long deduplicatedUsers = Interlocked.Exchange(ref DeduplicatedUsers, 0);
            long dataSince = Interlocked.Exchange(ref DataSince, currentTime.ToBinary());

            var statEvent = LdValue.BuildObject();
            AddDiagnosticCommonFields(statEvent, "diagnostic", currentTime);
            statEvent.Add("eventsInQueue", eventsInQueue);
            statEvent.Add("dataSinceDate", Util.GetUnixTimestampMillis(DateTime.FromBinary(dataSince)));
            statEvent.Add("droppedEvents", droppedEvents);
            statEvent.Add("deduplicatedUsers", deduplicatedUsers);
            lock (StreamInitsLock) {
                statEvent.Add("streamInits", StreamInits.Build());
                StreamInits = LdValue.BuildArray();
            }

            return new DiagnosticEvent(statEvent.Build());
        }

        #endregion
    }
}
