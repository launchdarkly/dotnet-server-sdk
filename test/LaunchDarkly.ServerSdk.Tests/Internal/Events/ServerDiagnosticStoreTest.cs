using System;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    public class ServerDiagnosticStoreTest : BaseTest
    {
        public ServerDiagnosticStoreTest(ITestOutputHelper testOutput) : base(testOutput) { }

        private IDiagnosticStore CreateDiagnosticStore(Action<ConfigurationBuilder> modConfig) {
            var builder = BasicConfig()
                .Http(Components.HttpConfiguration().Wrapper("Xamarin", "1.0.0"))
                .StartWaitTime(TimeSpan.Zero);
            if (!(modConfig is null))
            {
                modConfig(builder);
            }
            var config = builder.Build();
            var httpConfig = config.HttpConfigurationFactory.CreateHttpConfiguration(BasicContext.Basic);
            return new ServerDiagnosticStore(config, BasicContext.Basic, httpConfig);
        }

        [Fact]
        public void PersistedEventIsNull()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            var persistedEvent = _serverDiagnosticStore.PersistedUnsentEvent;
            Assert.Null(persistedEvent);
        }

        [Fact]
        public void PeriodicEventDefaultValuesAreCorrect()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            DateTime dataSince = _serverDiagnosticStore.DataSince;
            LdValue periodicEvent = _serverDiagnosticStore.CreateEventAndReset().JsonValue;

            Assert.Equal("diagnostic", periodicEvent.Get("kind").AsString);
            Assert.Equal(UnixMillisecondTime.FromDateTime(dataSince).Value, periodicEvent.Get("dataSinceDate").AsLong);
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
            Assert.Equal(UnixMillisecondTime.FromDateTime(timestamp).Value, streamInit.Get("timestamp").AsLong);
            Assert.Equal(200, streamInit.Get("durationMillis").AsInt);
            Assert.True(streamInit.Get("failed").AsBool);
        }

        [Fact]
        public void DataSinceFromLastDiagnostic()
        {
            IDiagnosticStore _serverDiagnosticStore = CreateDiagnosticStore(null);
            DiagnosticEvent periodicEvent = _serverDiagnosticStore.CreateEventAndReset();
            Assert.Equal(periodicEvent.JsonValue.Get("creationDate").AsLong,
                UnixMillisecondTime.FromDateTime(_serverDiagnosticStore.DataSince).Value);
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