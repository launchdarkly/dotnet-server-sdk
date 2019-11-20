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
        private LdValue _expectedPlatform = LdValue.BuildObject().Add("name", "dotnet").Build();
        private LdValue _expectedSdk = LdValue.BuildObject()
            .Add("name", "dotnet-server-sdk")
            .Add("version", ServerSideClientEnvironment.Instance.Version.ToString())
            .Add("wrapperName", "Xamarin")
            .Add("wrapperVersion", "1.0.0")
            .Build();
        private LdValue _expectedConfig = LdValue.BuildObject()
            .Add("customBaseURI", true)
            .Add("customEventsURI", false)
            .Add("customStreamURI", false)
            .Add("eventsCapacity", 10000)
            .Add("connectTimeoutMillis", 10000L)
            .Add("socketTimeoutMillis", 300000L)
            .Add("eventsFlushIntervalMillis", 5000L)
            .Add("usingProxy", false)
            .Add("usingProxyAuthenticator", false)
            .Add("streamingDisabled", true)
            .Add("usingRelayDaemon", false)
            .Add("offline", false)
            .Add("allAttributesPrivate", false)
            .Add("eventReportingDisabled", false)
            .Add("pollingIntervalMillis", 30000L)
            .Add("startWaitMillis", 0L)
            .Add("samplingInterval", 0)
            .Add("reconnectTimeMillis", 1000L)
            .Add("userKeysCapacity", 1000)
            .Add("userKeysFlushIntervalMillis", 300000L)
            .Add("inlineUsersInEvents", false)
            .Add("diagnosticRecordingIntervalMillis", 900000L)
            .Build();

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
            var persistedEvent = _serverDiagnosticStore.PersistedUnsentEvent;
            Assert.Null(persistedEvent);
        }

        [Fact]
        public void InitEventFieldsAreCorrect()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            Assert.NotNull(_serverDiagnosticStore.InitEvent);
            LdValue initEvent = _serverDiagnosticStore.InitEvent.Value.JsonValue;

            Assert.Equal("diagnostic-init", initEvent.Get("kind").AsString);
            Assert.Equal(_expectedPlatform, initEvent.Get("platform"));
            Assert.Equal(_expectedSdk, initEvent.Get("sdk"));
            Assert.Equal(_expectedConfig, initEvent.Get("configuration"));
            Assert.Equal("DK_KEY", initEvent.Get("id").Get("sdkKeySuffix").AsString);
            Assert.Equal(Util.GetUnixTimestampMillis(_serverDiagnosticStore.DataSince), initEvent.Get("creationDate").AsLong);
        }

        [Fact]
        public void PeriodicEventDefaultValuesAreCorrect()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            DateTime dataSince = _serverDiagnosticStore.DataSince;
            LdValue periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4).JsonValue;

            Assert.Equal("diagnostic", periodicEvent.Get("kind").AsString);
            Assert.Equal(Util.GetUnixTimestampMillis(dataSince), periodicEvent.Get("dataSinceDate").AsLong);
            Assert.Equal(4, periodicEvent.Get("eventsInQueue").AsInt);
            Assert.Equal(0, periodicEvent.Get("droppedEvents").AsInt);
            Assert.Equal(0, periodicEvent.Get("deduplicatedUsers").AsInt);

            LdValue streamInits = periodicEvent.Get("streamInits");
            Assert.Equal(0, streamInits.Count);
        }

        [Fact]
        public void PeriodicEventUsesIdFromInit()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            DiagnosticEvent? initEvent = _serverDiagnosticStore.InitEvent;
            Assert.True(initEvent.HasValue);
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);
            Assert.Equal(initEvent.Value.JsonValue.Get("id"), periodicEvent.JsonValue.Get("id"));
        }

        [Fact]
        public void CanIncrementDeduplicateUsers()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            _serverDiagnosticStore.IncrementDeduplicatedUsers();
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);
            Assert.Equal(1, periodicEvent.JsonValue.Get("deduplicatedUsers").AsInt);
        }

        [Fact]
        public void CanIncrementDroppedEvents()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            _serverDiagnosticStore.IncrementDroppedEvents();
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);
            Assert.Equal(1, periodicEvent.JsonValue.Get("droppedEvents").AsInt);
        }

        [Fact]
        public void CanAddStreamInit()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            DateTime timestamp = DateTime.Now;
            _serverDiagnosticStore.AddStreamInit(timestamp, TimeSpan.FromMilliseconds(200.0), true);
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);

            LdValue streamInits = periodicEvent.JsonValue.Get("streamInits");
            Assert.Equal(1, streamInits.Count);

            LdValue streamInit = streamInits.Get(0);
            Assert.Equal(Util.GetUnixTimestampMillis(timestamp), streamInit.Get("timestamp").AsLong);
            Assert.Equal(200, streamInit.Get("durationMillis").AsInt);
            Assert.Equal(true, streamInit.Get("failed").AsBool);
        }

        [Fact]
        public void DataSinceFromLastDiagnostic()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);
            Assert.Equal(periodicEvent.JsonValue.Get("creationDate").AsLong,
                Util.GetUnixTimestampMillis(_serverDiagnosticStore.DataSince));
        }

        [Fact]
        public void CreatingEventResetsFields()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore();
            _serverDiagnosticStore.IncrementDroppedEvents();
            _serverDiagnosticStore.IncrementDeduplicatedUsers();
            _serverDiagnosticStore.AddStreamInit(DateTime.Now, TimeSpan.FromMilliseconds(200.0), true);
            LdValue firstPeriodicEvent = _serverDiagnosticStore.CreateEventAndReset(4).JsonValue;
            LdValue nextPeriodicEvent = _serverDiagnosticStore.CreateEventAndReset(0).JsonValue;

            Assert.Equal(firstPeriodicEvent.Get("creationDate"), nextPeriodicEvent.Get("dataSinceDate"));
            Assert.Equal(0, nextPeriodicEvent.Get("eventsInQueue").AsInt);
            Assert.Equal(0, nextPeriodicEvent.Get("droppedEvents").AsInt);
            Assert.Equal(0, nextPeriodicEvent.Get("deduplicatedUsers").AsInt);

            LdValue streamInits = nextPeriodicEvent.Get("streamInits");
            Assert.Equal(0, streamInits.Count);
        }
    }
}