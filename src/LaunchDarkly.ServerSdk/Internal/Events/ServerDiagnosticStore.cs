using System;
using System.Collections.Generic;
using System.Threading;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    internal class ServerDiagnosticStore : IDiagnosticStore
    {
        private readonly Configuration Config;
        private readonly BasicConfiguration BasicConfig;
        private readonly IHttpConfiguration HttpConfig;
        private readonly DiagnosticEvent InitEvent;
        private readonly DiagnosticId DiagnosticId;

        // DataSince is stored in the "binary" long format so Interlocked.Exchange can be used
        private long DataSince;
        private long DroppedEvents;
        private long DeduplicatedUsers;
        private long EventsInLastBatch;
        private readonly object StreamInitsLock = new object();
        private LdValue.ArrayBuilder StreamInits = LdValue.BuildArray();

        #region IDiagnosticStore interface properties
        DiagnosticEvent? IDiagnosticStore.InitEvent => InitEvent;
        DiagnosticEvent? IDiagnosticStore.PersistedUnsentEvent => null;
        DateTime IDiagnosticStore.DataSince => DateTime.FromBinary(Interlocked.Read(ref DataSince));
        #endregion

        internal ServerDiagnosticStore(Configuration config, BasicConfiguration basicConfig, IHttpConfiguration httpConfig)
        {
            DateTime currentTime = DateTime.Now;
            Config = config;
            BasicConfig = basicConfig;
            HttpConfig = httpConfig;
            DataSince = currentTime.ToBinary();
            DiagnosticId = new DiagnosticId(config.SdkKey, Guid.NewGuid());
            InitEvent = BuildInitEvent(currentTime);
        }

        private void AddDiagnosticCommonFields(LdValue.ObjectBuilder fieldsBuilder, string kind, DateTime creationDate)
        {
            fieldsBuilder.Add("kind", kind);
            fieldsBuilder.Add("id", EncodeDiagnosticId(DiagnosticId));
            fieldsBuilder.Add("creationDate", UnixMillisecondTime.FromDateTime(creationDate).Value);
        }

        private LdValue EncodeDiagnosticId(DiagnosticId id)
        {
            var o = LdValue.BuildObject().Add("diagnosticId", id.Id.ToString());
            if (id.SdkKeySuffix != null)
            {
                o.Add("sdkKeySuffix", id.SdkKeySuffix);
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
                .Add("version", AssemblyVersions.GetAssemblyVersionStringForType(typeof(LdClient)));
            foreach (var kv in HttpConfig.DefaultHeaders)
            {
                if (kv.Key.ToLower() == "x-launchdarkly-wrapper")
                {
                    if (kv.Value.Contains("/"))
                    {
                        sdkInfo.Add("wrapperName", kv.Value.Substring(0, kv.Value.IndexOf("/")));
                        sdkInfo.Add("wrapperVersion", kv.Value.Substring(kv.Value.IndexOf("/") + 1));
                    }
                    else
                    {
                        sdkInfo.Add("wrapperName", kv.Value);
                    }
                }
            }
            return sdkInfo.Build();
        }

        private LdValue InitEventConfig()
        {
            var configInfo = LdValue.BuildObject();
            configInfo.Add("startWaitMillis", (long)Config.StartWaitTime.TotalMilliseconds);

            // Allow each pluggable component to describe its own relevant properties.
            MergeComponentProperties(configInfo, Config.DataStoreFactory ?? Components.InMemoryDataStore, "dataStoreType");
            MergeComponentProperties(configInfo, Config.DataSourceFactory ?? Components.StreamingDataSource(), null);
            MergeComponentProperties(configInfo, Config.EventProcessorFactory ?? Components.SendEvents(), null);
            MergeComponentProperties(configInfo, Config.HttpConfigurationFactory ?? Components.HttpConfiguration(), null);

            return configInfo.Build();
        }

        private void MergeComponentProperties(LdValue.ObjectBuilder builder, object component,
            string defaultPropertyName)
        {
            if (!(component is IDiagnosticDescription))
            {
                if (!string.IsNullOrEmpty(defaultPropertyName))
                {
                    builder.Add(defaultPropertyName, "custom");
                }
                return;
            }
            var componentDesc = (component as IDiagnosticDescription).DescribeConfiguration(BasicConfig);
            if (!string.IsNullOrEmpty(defaultPropertyName))
            {
                builder.Add(defaultPropertyName, componentDesc.IsString ? componentDesc : LdValue.Of("custom"));
            }
            else if (componentDesc.Type == LdValueType.Object)
            {
                foreach (KeyValuePair<string, LdValue> prop in componentDesc.AsDictionary(LdValue.Convert.Json))
                {
                    builder.Add(prop.Key, prop.Value); // TODO: filter allowable properties
                }
            }
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
            streamInitObject.Add("timestamp", UnixMillisecondTime.FromDateTime(timestamp).Value);
            streamInitObject.Add("durationMillis", duration.TotalMilliseconds);
            streamInitObject.Add("failed", failed);
            lock (StreamInitsLock)
            {
                StreamInits.Add(streamInitObject.Build());
            }
        }

        public void RecordEventsInBatch(long eventsInBatch)
        {
            Interlocked.Exchange(ref EventsInLastBatch, eventsInBatch);
        }

        public DiagnosticEvent CreateEventAndReset()
        {
            DateTime currentTime = DateTime.Now;
            long droppedEvents = Interlocked.Exchange(ref DroppedEvents, 0);
            long deduplicatedUsers = Interlocked.Exchange(ref DeduplicatedUsers, 0);
            long eventsInLastBatch = Interlocked.Exchange(ref EventsInLastBatch, 0);
            long dataSince = Interlocked.Exchange(ref DataSince, currentTime.ToBinary());

            var statEvent = LdValue.BuildObject();
            AddDiagnosticCommonFields(statEvent, "diagnostic", currentTime);
            statEvent.Add("eventsInLastBatch", eventsInLastBatch);
            statEvent.Add("dataSinceDate", UnixMillisecondTime.FromDateTime(DateTime.FromBinary(dataSince)).Value);
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
