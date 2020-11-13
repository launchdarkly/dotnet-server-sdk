using System;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Internal.Helpers;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    public class ServerDiagnosticStoreTest
    {
        private const string sdkKey = "SDK_KEY";
        private static readonly LdValue _expectedPlatform = LdValue.BuildObject().Add("name", "dotnet").Build();
        private static readonly LdValue _expectedSdk = LdValue.BuildObject()
            .Add("name", "dotnet-server-sdk")
            .Add("version", ServerSideClientEnvironment.Instance.Version.ToString())
            .Add("wrapperName", "Xamarin")
            .Add("wrapperVersion", "1.0.0")
            .Build();
        private static readonly LdValue _expectedConfig = LdValue.BuildObject()
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
            .Add("reconnectTimeMillis", 1000L)
            .Add("userKeysCapacity", 1000)
            .Add("userKeysFlushIntervalMillis", 300000L)
            .Add("inlineUsersInEvents", false)
            .Add("diagnosticRecordingIntervalMillis", 900000L)
            .Build();

        private LdValue.ObjectBuilder ExpectedDefaultPropertiesWithStreaming() =>
            ExpectedDefaultPropertiesWithoutDataSource()
            .Add("customBaseURI", false)
            .Add("customStreamURI", false)
            .Add("streamingDisabled", false)
            .Add("reconnectTimeMillis", 1000)
            .Add("usingRelayDaemon", false);

        private LdValue.ObjectBuilder ExpectedDefaultPropertiesWithoutDataSource() =>
            LdValue.BuildObject()
            .Add("customEventsURI", false)
            .Add("eventsCapacity", 10000)
            .Add("connectTimeoutMillis", 10000L)
            .Add("socketTimeoutMillis", 300000L)
            .Add("eventsFlushIntervalMillis", 5000L)
            .Add("usingProxy", false)
            .Add("usingProxyAuthenticator", false)
            .Add("offline", false)
            .Add("allAttributesPrivate", false)
            .Add("eventReportingDisabled", false)
            .Add("startWaitMillis", 0L)
            .Add("userKeysCapacity", 1000)
            .Add("userKeysFlushIntervalMillis", 300000L)
            .Add("inlineUsersInEvents", false)
            .Add("diagnosticRecordingIntervalMillis", 900000L);

        private IDiagnosticStore CreateDiagnosticStore(Action<IConfigurationBuilder> modConfig) {
            var builder = Configuration.Builder(sdkKey)
                .StartWaitTime(TimeSpan.Zero)
                .WrapperName("Xamarin")
                .WrapperVersion("1.0.0");
            if (!(modConfig is null))
            {
                modConfig(builder);
            }
            var config = builder.Build();
            return new ServerDiagnosticStore(config, new BasicConfiguration(sdkKey, false, TestUtils.NullLogger));
        }

        [Fact]
        public void PersistedEventIsNull()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            var persistedEvent = _serverDiagnosticStore.PersistedUnsentEvent;
            Assert.Null(persistedEvent);
        }

        [Fact]
        public void InitEventFieldsOtherThanConfig()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            Assert.NotNull(_serverDiagnosticStore.InitEvent);
            LdValue initEvent = _serverDiagnosticStore.InitEvent.Value.JsonValue;

            Assert.Equal("diagnostic-init", initEvent.Get("kind").AsString);
            Assert.Equal(_expectedPlatform, initEvent.Get("platform"));
            Assert.Equal(_expectedSdk, initEvent.Get("sdk"));
            Assert.Equal("DK_KEY", initEvent.Get("id").Get("sdkKeySuffix").AsString);
            Assert.Equal(Util.GetUnixTimestampMillis(_serverDiagnosticStore.DataSince), initEvent.Get("creationDate").AsLong);
        }

        [Fact]
        public void DefaultDiagnosticConfiguration()
        {
            var _serverDiagnosticStore = CreateDiagnosticStore(null);
            var initEvent = _serverDiagnosticStore.InitEvent.Value.JsonValue;
            var expected = ExpectedDefaultPropertiesWithStreaming().Build();
            Assert.Equal(expected, initEvent.Get("configuration"));
        }

        [Fact]
        public void CustomDiagnosticConfigurationGeneralProperties()
        {
            var diagStore = CreateDiagnosticStore(c =>
            {
                c.AllAttributesPrivate(true)
                    .ConnectionTimeout(TimeSpan.FromMilliseconds(1001))
                    .DiagnosticRecordingInterval(TimeSpan.FromMinutes(45))
                    .EventCapacity(999)
                    .EventFlushInterval(TimeSpan.FromMilliseconds(1002))
                    .InlineUsersInEvents(true)
                    .ReadTimeout(TimeSpan.FromMilliseconds(1003))
                    .StartWaitTime(TimeSpan.FromSeconds(10))
                    .UserKeysCapacity(799)
                    .UserKeysFlushInterval(TimeSpan.FromMilliseconds(1004));
            });
            var expected = LdValue.BuildObject()
                .Add("customBaseURI", false)
                .Add("customEventsURI", false)
                .Add("customStreamURI", false)
                .Add("reconnectTimeMillis", 1000)
                .Add("streamingDisabled", false)
                .Add("allAttributesPrivate", true)
                .Add("connectTimeoutMillis", 1001)
                .Add("diagnosticRecordingIntervalMillis", 45 * 60 * 1000)
                .Add("eventReportingDisabled", false)
                .Add("eventsCapacity", 999)
                .Add("eventsFlushIntervalMillis", 1002)
                .Add("inlineUsersInEvents", true)
                .Add("socketTimeoutMillis", 1003)
                .Add("offline", false)
                .Add("startWaitMillis", 10000)
                .Add("userKeysCapacity", 799)
                .Add("userKeysFlushIntervalMillis", 1004)
                .Add("usingProxy", false)
                .Add("usingProxyAuthenticator", false)
                .Add("usingRelayDaemon", false)
                .Build();
            Assert.Equal(expected, GetConfigData(diagStore));
        }

        [Fact]
        public void CustomDiagnosticConfigurationForStreaming()
        {
            var diagStore = CreateDiagnosticStore(c =>
            {
                c.DataSource(
                    Components.StreamingDataSource()
                        .BaseUri(new Uri("http://custom"))
                        .InitialReconnectDelay(TimeSpan.FromSeconds(2))
                    );
            });
            var expected = ExpectedDefaultPropertiesWithoutDataSource()
                .Add("customBaseURI", false)
                .Add("customStreamURI", true)
                .Add("streamingDisabled", false)
                .Add("reconnectTimeMillis", 2000)
                .Add("usingRelayDaemon", false)
                .Build();
            Assert.Equal(expected, GetConfigData(diagStore));
        }

        [Fact]
        public void CustomDiagnosticConfigurationForPolling()
        {
            var diagStore1 = CreateDiagnosticStore(c =>
            {
                c.DataSource(
                    Components.PollingDataSource()
                        .PollInterval(TimeSpan.FromSeconds(45))
                    );
            });
            var expected1 = ExpectedDefaultPropertiesWithoutDataSource()
                .Add("customBaseURI", false)
                .Add("customStreamURI", false)
                .Add("pollingIntervalMillis", 45000)
                .Add("streamingDisabled", true)
                .Add("usingRelayDaemon", false)
                .Build();
            Assert.Equal(expected1, GetConfigData(diagStore1));

            var diagStore2 = CreateDiagnosticStore(c =>
            {
                c.DataSource(
                    Components.PollingDataSource()
                        .BaseUri(new Uri("http://custom"))
                        .PollInterval(TimeSpan.FromSeconds(45))
                    );
            });
            var expected2 = ExpectedDefaultPropertiesWithoutDataSource()
                .Add("customBaseURI", true)
                .Add("customStreamURI", false)
                .Add("pollingIntervalMillis", 45000)
                .Add("streamingDisabled", true)
                .Add("usingRelayDaemon", false)
                .Build();
            Assert.Equal(expected2, GetConfigData(diagStore2));
        }

        [Fact]
        public void CustomDiagnosticConfigurationForExternalUpdatesOnly()
        {
            var diagStore = CreateDiagnosticStore(c =>
            {
                c.DataSource(Components.ExternalUpdatesOnly);
            });
            var expected = ExpectedDefaultPropertiesWithoutDataSource()
                .Add("customBaseURI", false)
                .Add("customStreamURI", false)
                .Add("streamingDisabled", false)
                .Add("usingRelayDaemon", true)
                .Build();
            Assert.Equal(expected, GetConfigData(diagStore));
        }

        private LdValue GetConfigData(IDiagnosticStore ds) =>
            ds.InitEvent.Value.JsonValue.Get("configuration");

        [Fact]
        public void PeriodicEventDefaultValuesAreCorrect()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            DateTime dataSince = _serverDiagnosticStore.DataSince;
            LdValue periodicEvent = _serverDiagnosticStore.CreateEventAndReset().JsonValue;

            Assert.Equal("diagnostic", periodicEvent.Get("kind").AsString);
            Assert.Equal(Util.GetUnixTimestampMillis(dataSince), periodicEvent.Get("dataSinceDate").AsLong);
            Assert.Equal(0, periodicEvent.Get("eventsInLastBatch").AsInt);
            Assert.Equal(0, periodicEvent.Get("droppedEvents").AsInt);
            Assert.Equal(0, periodicEvent.Get("deduplicatedUsers").AsInt);

            LdValue streamInits = periodicEvent.Get("streamInits");
            Assert.Equal(0, streamInits.Count);
        }

        [Fact]
        public void PeriodicEventUsesIdFromInit()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            DiagnosticEvent? initEvent = _serverDiagnosticStore.InitEvent;
            Assert.True(initEvent.HasValue);
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset();
            Assert.Equal(initEvent.Value.JsonValue.Get("id"), periodicEvent.JsonValue.Get("id"));
        }

        [Fact]
        public void CanIncrementDeduplicateUsers()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            _serverDiagnosticStore.IncrementDeduplicatedUsers();
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset();
            Assert.Equal(1, periodicEvent.JsonValue.Get("deduplicatedUsers").AsInt);
        }

        [Fact]
        public void CanIncrementDroppedEvents()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            _serverDiagnosticStore.IncrementDroppedEvents();
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset();
            Assert.Equal(1, periodicEvent.JsonValue.Get("droppedEvents").AsInt);
        }

        [Fact]
        public void CanRecordEventsInBatch()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            _serverDiagnosticStore.RecordEventsInBatch(4);
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset();
            Assert.Equal(4, periodicEvent.JsonValue.Get("eventsInLastBatch").AsInt);
        }

        [Fact]
        public void CanAddStreamInit()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            DateTime timestamp = DateTime.Now;
            _serverDiagnosticStore.AddStreamInit(timestamp, TimeSpan.FromMilliseconds(200.0), true);
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset();

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
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset();
            Assert.Equal(periodicEvent.JsonValue.Get("creationDate").AsLong,
                Util.GetUnixTimestampMillis(_serverDiagnosticStore.DataSince));
        }

        [Fact]
        public void CreatingEventResetsFields()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            _serverDiagnosticStore.IncrementDroppedEvents();
            _serverDiagnosticStore.IncrementDeduplicatedUsers();
            _serverDiagnosticStore.RecordEventsInBatch(10);
            _serverDiagnosticStore.AddStreamInit(DateTime.Now, TimeSpan.FromMilliseconds(200.0), true);
            LdValue firstPeriodicEvent = _serverDiagnosticStore.CreateEventAndReset().JsonValue;
            LdValue nextPeriodicEvent = _serverDiagnosticStore.CreateEventAndReset().JsonValue;

            Assert.Equal(firstPeriodicEvent.Get("creationDate"), nextPeriodicEvent.Get("dataSinceDate"));
            Assert.Equal(0, nextPeriodicEvent.Get("eventsInLastBatch").AsInt);
            Assert.Equal(0, nextPeriodicEvent.Get("droppedEvents").AsInt);
            Assert.Equal(0, nextPeriodicEvent.Get("deduplicatedUsers").AsInt);
            Assert.Equal(0, nextPeriodicEvent.Get("eventsInLastBatch").AsInt);
            LdValue streamInits = nextPeriodicEvent.Get("streamInits");
            Assert.Equal(0, streamInits.Count);
        }
    }
}