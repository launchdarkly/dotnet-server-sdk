using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        private readonly HttpConfiguration HttpConfig;
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

        internal ServerDiagnosticStore(Configuration config, BasicConfiguration basicConfig, HttpConfiguration httpConfig)
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
            LdValue.BuildObject()
                .Add("name", "dotnet")
                .Add("dotNetTargetFramework", LdValue.Of(GetDotNetTargetFramework()))
                .Add("osName", LdValue.Of(GetOSName()))
                .Add("osVersion", LdValue.Of(GetOSVersion()))
                .Add("osArch", LdValue.Of(GetOSArch()))
                .Build();

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

        internal static string GetOSName()
        {
            // Environment.OSVersion.Platform is another way to get this information, except that it does not
            // reliably distinguish between MacOS and Linux.

#if NET452
            // .NET Framework 4.5 does not support RuntimeInformation.ISOSPlatform. We could use Environment.OSVersion.Platform
            // instead (it's similar, except that it can't reliably distinguish between MacOS and Linux)... but .NET 4.5 can't
            // run on anything but Windows anyway.
            return "Windows";
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "MacOS";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "Windows";
            }
            return "unknown";
#endif
        }

        internal static string GetOSVersion()
        {
            // .NET's way of reporting Windows versions is very idiosyncratic, e.g. Windows 8 is "6.2", but we'll
            // just report what it says and translate it later when we look at the analytics.
            return Environment.OSVersion.Version.ToString();
        }

        internal static string GetOSArch()
        {
#if NET452
            // .NET Standard 4.5 does not support RuntimeInformation.OSArchitecture
            return "unknown";
#else
            return RuntimeInformation.OSArchitecture.ToString().ToLower(); // "arm", "arm64", "x64", "x86"
#endif
        }

        internal static string GetDotNetTargetFramework()
        {
            // Note that this is the _target framework_ that was selected at build time based on the application's
            // compatibility requirements; it doesn't tell us anything about the actual OS version. We'll need to
            // update this whenever we add or remove supported target frameworks in the .csproj file.
#if NETSTANDARD2_0
            return "netstandard2.0";
#elif NET452
            return "net452";
#elif NET471
            return "net471";
#else
            return "unknown";
#endif
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
