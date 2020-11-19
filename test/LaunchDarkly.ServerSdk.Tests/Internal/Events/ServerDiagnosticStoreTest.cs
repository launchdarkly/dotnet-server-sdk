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

        private LdValue GetConfigData(IDiagnosticStore ds) =>
            ds.InitEvent.Value.JsonValue.Get("configuration");

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
            var expected = ExpectedConfigProps.Base()
                .WithEventsDefaults()
                .WithStreamingDefaults()
                .Build();
            TestUtils.AssertJsonEqual(expected, initEvent.Get("configuration"));
        }

        [Fact]
        public void CustomDiagnosticConfigurationGeneralProperties()
        {
            var diagStore = CreateDiagnosticStore(c =>
            {
                c.ConnectionTimeout(TimeSpan.FromMilliseconds(1001))
                    .ReadTimeout(TimeSpan.FromMilliseconds(1003))
                    .StartWaitTime(TimeSpan.FromSeconds(10));
            });
            var expected = LdValue.BuildObject()
                .WithEventsDefaults()
                .WithStreamingDefaults()
                .Add("connectTimeoutMillis", 1001)
                .Add("socketTimeoutMillis", 1003)
                .Add("startWaitMillis", 10000)
                .Add("usingProxy", false)
                .Add("usingProxyAuthenticator", false)
                .Build();
            TestUtils.AssertJsonEqual(expected, GetConfigData(diagStore));
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
            var expected = ExpectedConfigProps.Base()
                .WithEventsDefaults()
                .Add("customBaseURI", false)
                .Add("customStreamURI", true)
                .Add("streamingDisabled", false)
                .Add("reconnectTimeMillis", 2000)
                .Add("usingRelayDaemon", false)
                .Build();
            TestUtils.AssertJsonEqual(expected, GetConfigData(diagStore));
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
            var expected1 = ExpectedConfigProps.Base()
                .WithEventsDefaults()
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
            var expected2 = ExpectedConfigProps.Base()
                .WithEventsDefaults()
                .Add("customBaseURI", true)
                .Add("customStreamURI", false)
                .Add("pollingIntervalMillis", 45000)
                .Add("streamingDisabled", true)
                .Add("usingRelayDaemon", false)
                .Build();
            TestUtils.AssertJsonEqual(expected2, GetConfigData(diagStore2));
        }

        [Fact]
        public void CustomDiagnosticConfigurationForExternalUpdatesOnly()
        {
            var diagStore = CreateDiagnosticStore(c =>
            {
                c.DataSource(Components.ExternalUpdatesOnly);
            });
            var expected = ExpectedConfigProps.Base()
                .WithEventsDefaults()
                .Add("customBaseURI", false)
                .Add("customStreamURI", false)
                .Add("streamingDisabled", false)
                .Add("usingRelayDaemon", true)
                .Build();
            TestUtils.AssertJsonEqual(expected, GetConfigData(diagStore));
        }

        [Fact]
        public void CustomDiagnosticConfigurationForEvents()
        {
            var diagStore = CreateDiagnosticStore(c =>
            {
                c.Events(
                    Components.SendEvents()
                        .AllAttributesPrivate(true)
                        .BaseUri(new Uri("http://custom"))
                        .Capacity(333)
                        .DiagnosticRecordingInterval(TimeSpan.FromMinutes(32))
                        .FlushInterval(TimeSpan.FromMilliseconds(555))
                        .InlineUsersInEvents(true)
                        .UserKeysCapacity(444)
                        .UserKeysFlushInterval(TimeSpan.FromMinutes(23))
                    );
            });
            var expected = ExpectedConfigProps.Base()
                .WithStreamingDefaults()
                .Add("allAttributesPrivate", true)
                .Add("customEventsURI", true)
                .Add("diagnosticRecordingIntervalMillis", TimeSpan.FromMinutes(32).TotalMilliseconds)
                .Add("eventsCapacity", 333)
                .Add("eventsFlushIntervalMillis", 555)
                .Add("inlineUsersInEvents", true)
                .Add("samplingInterval", 0) // obsolete, no way to set this
                .Add("userKeysCapacity", 444)
                .Add("userKeysFlushIntervalMillis", TimeSpan.FromMinutes(23).TotalMilliseconds)
                .Build();
            TestUtils.AssertJsonEqual(expected, GetConfigData(diagStore));
        }

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

    static class ExpectedConfigProps
    {
        public static LdValue.ObjectBuilder Base() =>
            LdValue.BuildObject()
                .Add("connectTimeoutMillis", 10000L)
                .Add("socketTimeoutMillis", 300000L)
                .Add("startWaitMillis", 0L)
                .Add("usingProxy", false)
                .Add("usingProxyAuthenticator", false);

        public static LdValue.ObjectBuilder WithStreamingDefaults(this LdValue.ObjectBuilder b) =>
            b.Add("customBaseURI", false)
                .Add("customStreamURI", false)
                .Add("streamingDisabled", false)
                .Add("reconnectTimeMillis", StreamingDataSourceBuilder.DefaultInitialReconnectDelay.TotalMilliseconds)
                .Add("usingRelayDaemon", false);

        public static LdValue.ObjectBuilder WithEventsDefaults(this LdValue.ObjectBuilder b) =>
            b.Add("allAttributesPrivate", false)
                .Add("customEventsURI", false)
                .Add("diagnosticRecordingIntervalMillis", EventProcessorBuilder.DefaultDiagnosticRecordingInterval.TotalMilliseconds)
                .Add("eventsCapacity", EventProcessorBuilder.DefaultCapacity)
                .Add("eventsFlushIntervalMillis", EventProcessorBuilder.DefaultFlushInterval.TotalMilliseconds)
                .Add("inlineUsersInEvents", false)
                .Add("samplingInterval", 0)
                .Add("userKeysCapacity", EventProcessorBuilder.DefaultUserKeysCapacity)
                .Add("userKeysFlushIntervalMillis", EventProcessorBuilder.DefaultUserKeysFlushInterval.TotalMilliseconds);
    }
}