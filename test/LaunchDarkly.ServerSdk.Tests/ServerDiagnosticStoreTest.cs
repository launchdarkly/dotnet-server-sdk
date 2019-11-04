using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using LaunchDarkly.Client;
using LaunchDarkly.Common;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class ServerDiagnosticStoreTest
    {
        private Dictionary<string, object> _expectedPlatform = new Dictionary<string, object> { { "name", "dotnet" } };
        private Dictionary<string, object> _expectedSdk = new Dictionary<string, object> {
            { "name", "dotnet-server-sdk" },
            { "version", ServerSideClientEnvironment.Instance.Version.ToString() },
            { "wrapperName", "Xamarin" },
            { "wrapperVersion", "1.0.0" }
        };
        private Dictionary<string, object> _expectedConfig = new Dictionary<string, object> {
            { "baseURI", "http://fake/" },
            { "eventsURI", "https://events.launchdarkly.com/" },
            { "streamURI", "https://stream.launchdarkly.com/" },
            { "eventsCapacity", 10000 },
            { "connectTimeoutMillis", 10000L },
            { "socketTimeoutMillis", 300000L },
            { "eventsFlushIntervalMillis", 5000L },
            { "usingProxy", false },
            { "usingProxyAuthenticator", false },
            { "streamingDisabled", true },
            { "usingRelayDaemon", false },
            { "offline", false },
            { "allAttributesPrivate", false },
            { "eventReportingDisabled", false },
            { "pollingIntervalMillis", 30000L },
            { "startWaitMillis", 0L },
            { "samplingInterval", 0 },
            { "reconnectTimeMillis", 1000L },
            { "userKeysCapacity", 1000 },
            { "userKeysFlushIntervalMillis", 300000L },
            { "inlineUsersInEvents", false },
            { "diagnosticRecordingIntervalMillis", 900000L }
        };

        private IDiagnosticStore CreateDiagnosticStore() {
            Configuration config = Configuration.Builder("SDK_KEY")
                .IsStreamingEnabled(false)
                .BaseUri(new Uri("http://fake"))
                .StartWaitTime(TimeSpan.Zero)
                .WrapperName("Xamarin")
                .WrapperVersion("1.0.0")
                .Build();
            return new ServerDiagnosticStore(config);
        }

        [Fact]
        public void PersistedEventIsNull()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            IReadOnlyDictionary<string, object> persistedEvent = _serverDiagnosticStore.PersistedUnsentEvent;
            Assert.Null(persistedEvent);
        }

        [Fact]
        public void DataSinceIsRecent()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            DateTime dataSince = _serverDiagnosticStore.DataSince;
            Assert.True((DateTime.Now - dataSince).Duration().TotalMilliseconds < 10);
        }

        [Fact]
        public void InitEventFieldsAreCorrect()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            IReadOnlyDictionary<string, object> initEvent = _serverDiagnosticStore.InitEvent;

            Assert.Equal("diagnostic-init", initEvent["kind"]);
            Assert.Equal(_expectedPlatform, initEvent["platform"]);
            Assert.Equal(_expectedSdk, initEvent["sdk"]);
            Assert.Equal(_expectedConfig, initEvent["configuration"]);
            Assert.Equal("DK_KEY", ((DiagnosticId) initEvent["id"])._sdkKeySuffix);
            Assert.Equal(Util.GetUnixTimestampMillis(_serverDiagnosticStore.DataSince), initEvent["creationDate"]);
        }

        [Fact]
        public void PeriodicEventDefaultValuesAreCorrect()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            DateTime dataSince = _serverDiagnosticStore.DataSince;
            IReadOnlyDictionary<string, object> periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);

            Assert.Equal("diagnostic", periodicEvent["kind"]);
            Assert.Equal(Util.GetUnixTimestampMillis(dataSince), periodicEvent["dataSinceDate"]);
            Assert.Equal(4L, periodicEvent["eventsInQueue"]);
            Assert.Equal(0L, periodicEvent["droppedEvents"]);
            Assert.Equal(0L, periodicEvent["deduplicatedUsers"]);

            List<Dictionary<string, object>> streamInits = (List<Dictionary<string, object>>) periodicEvent["streamInits"];
            Assert.True(streamInits.Count == 0);
        }

        [Fact]
        public void PeriodicEventUsesIdFromInit()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            IReadOnlyDictionary<string, object> initEvent = _serverDiagnosticStore.InitEvent;
            IReadOnlyDictionary<string, object> periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);
            Assert.Equal(initEvent["id"], periodicEvent["id"]);
        }

        [Fact]
        public void CanIncrementDeduplicateUsers()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            _serverDiagnosticStore.IncrementDeduplicatedUsers();
            IReadOnlyDictionary<string, object> periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);
            Assert.Equal(1L, periodicEvent["deduplicatedUsers"]);
        }

        [Fact]
        public void CanIncrementDroppedEvents()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            _serverDiagnosticStore.IncrementDroppedEvents();
            IReadOnlyDictionary<string, object> periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);
            Assert.Equal(1L, periodicEvent["droppedEvents"]);
        }

        [Fact]
        public void CanAddStreamInit()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            DateTime timestamp = DateTime.Now;
            _serverDiagnosticStore.AddStreamInit(timestamp, TimeSpan.FromMilliseconds(200.0), true);
            IReadOnlyDictionary<string, object> periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);

            List<Dictionary<string, object>> streamInits = (List<Dictionary<string, object>>) periodicEvent["streamInits"];
            Assert.True(streamInits.Count == 1);

            Dictionary<string, object> streamInit = streamInits[0];
            Assert.Equal(Util.GetUnixTimestampMillis(timestamp), (long)streamInit["timestamp"]);
            Assert.Equal(200.0, streamInit["durationMillis"]);
            Assert.Equal(true, streamInit["failed"]);
        }

        [Fact]
        public void DataSinceFromLastDiagnostic()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            IReadOnlyDictionary<string, object> periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);
            Assert.Equal(periodicEvent["creationDate"], Util.GetUnixTimestampMillis(_serverDiagnosticStore.DataSince));
        }

        [Fact]
        public void CreatingEventResetsFields()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            _serverDiagnosticStore.IncrementDroppedEvents();
            _serverDiagnosticStore.IncrementDeduplicatedUsers();
            _serverDiagnosticStore.AddStreamInit(DateTime.Now, TimeSpan.FromMilliseconds(200.0), true);
            IReadOnlyDictionary<string, object> firstPeriodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);
            IReadOnlyDictionary<string, object> nextPeriodicEvent = _serverDiagnosticStore.CreateEventAndReset(0);

            Assert.Equal(firstPeriodicEvent["creationDate"], nextPeriodicEvent["dataSinceDate"]);
            Assert.Equal(0L, nextPeriodicEvent["eventsInQueue"]);
            Assert.Equal(0L, nextPeriodicEvent["droppedEvents"]);
            Assert.Equal(0L, nextPeriodicEvent["deduplicatedUsers"]);

            List<Dictionary<string, object>> streamInits = (List<Dictionary<string, object>>) nextPeriodicEvent["streamInits"];
            Assert.True(streamInits.Count == 0);
        }
    }
}