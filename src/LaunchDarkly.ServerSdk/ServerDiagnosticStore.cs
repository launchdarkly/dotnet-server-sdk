using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using LaunchDarkly.Client.Interfaces;
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
        private long EventsInLastBatch;
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

        private LdValue InitEventPlatform()
        {
            return LdValue.BuildObject()
                .Add("name", "dotnet")
                .Add("dotNetTargetFramework", LdValue.Of(GetDotNetTargetFramework()))
                .Add("osName", LdValue.Of(GetOSName()))
                .Add("osVersion", LdValue.Of(GetOSVersion()))
                .Add("osArch", LdValue.Of(GetOSArch()))
                .Build();
        }

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
            configInfo.Add("connectTimeoutMillis", Config.HttpClientTimeout.TotalMilliseconds);
            configInfo.Add("socketTimeoutMillis", Config.ReadTimeout.TotalMilliseconds);
            configInfo.Add("usingProxy", false);
            configInfo.Add("usingProxyAuthenticator", false);
            configInfo.Add("offline", Config.Offline);
            configInfo.Add("startWaitMillis", (long)Config.StartWaitTime.TotalMilliseconds);
            configInfo.Add("dataStoreType", NormalizeDataStoreType(Config.FeatureStoreFactory));

            // Allow each pluggable component to describe its own relevant properties.
#pragma warning disable CS0618 // using obsolete API
            MergeComponentProperties(configInfo, Config.DataSource ?? Components.DefaultUpdateProcessor, null);
            MergeComponentProperties(configInfo, Config.EventProcessorFactory ?? Components.DefaultEventProcessor, null);
#pragma warning restore CS0618

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
            var componentDesc = (component as IDiagnosticDescription).DescribeConfiguration(Config);
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

        private string NormalizeDataStoreType(IFeatureStoreFactory storeFactory)
        {
            if (storeFactory is null)
            {
                return "memory";
            }
            var typeName = storeFactory.GetType().Name;
            switch (typeName)
            {
                // These hard-coded tests will eventually be replaced by an interface that lets components describe themselves.
                case "InMemoryFeatureStoreFactory":
                    return "memory";
                case "ConsulFeatureStoreBuilder":
                    return "Consul";
                case "DynamoFeatureStoreBuilder":
                    return "Dynamo";
                case "RedisFeatureStoreBuilder":
                    return "Redis";
            }
            return "custom";
        }

        internal static string GetOSName() {
            // Environment.OSVersion.Platform is another way to get this information, except that it does not
            // reliably distinguish between MacOS and Linux.

#if NET45
            // .NET Framework 4.5 does not support RuntimeInformation.ISOSPlatform. We could use Environment.OSVersion.Platform
            // instead (it's similar, except that it can't reliably distinguish between MacOS and Linux)... but .NET 4.5 can't
            // run on anything but Windows anyway.
            return "Windows";
#else
            // .NET Standard <2.0 does not support Environment.OSVersion; instead, use System.Runtime.Interopservices
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return "Linux";
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return "MacOS";
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return "Windows";
            }
            return "unknown";
#endif
        }

        internal static string GetOSVersion() {
#if NETSTANDARD1_4 || NETSTANDARD1_6
            // .NET Standard <2.0 has no equivalent of Environment.OSVersion.Version
            return "unknown";
#else
            // .NET's way of reporting Windows versions is very idiosyncratic, e.g. Windows 8 is "6.2", but we'll
            // just report what it says and translate it later when we look at the analytics.
            return Environment.OSVersion.Version.ToString();
#endif
        }

        internal static string GetOSArch() {
#if NET45
            // .NET Standard 4.5 does not support RuntimeInformation.OSArchitecture
            return "unknown";
#else
            return RuntimeInformation.OSArchitecture.ToString().ToLower(); // "arm", "arm64", "x64", "x86"
#endif
        }

        internal static string GetDotNetTargetFramework() {
            // Note that this is the _target framework_ that was selected at build time based on the application's
            // compatibility requirements; it doesn't tell us anything about the actual OS version. We'll need to
            // update this whenever we add or remove supported target frameworks in the .csproj file.
#if NETSTANDARD1_4
            return "netstandard1.4";
#elif NETSTANDARD1_6
            return "netstandard1.6";
#elif NETSTANDARD2_0
            return "netstandard2.0";
#elif NET45
            return "net45";
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
            streamInitObject.Add("timestamp", Util.GetUnixTimestampMillis(timestamp));
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
