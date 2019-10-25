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
        [Fact]
        public void InitEventFieldsAreCorrect() {
            Configuration config = Configuration.Builder("SDK_KEY")
                .IsStreamingEnabled(false)
                .BaseUri(new Uri("http://fake"))
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            IDiagnosticStore _serverDiagnosticStore = new ServerDiagnosticStore(config);
            IReadOnlyDictionary<string, object> InitEvent = _serverDiagnosticStore.InitEvent;
            Assert.Equal("diagnostic-init", InitEvent["kind"]);
            DiagnosticId Id = (DiagnosticId) InitEvent["id"];
            Assert.Equal("DK_KEY", Id._sdkKeySuffix);
            long TimeDifference = Util.GetUnixTimestampMillis(DateTime.Now) - (long) InitEvent["creationDate"];
            Assert.True(TimeDifference < 50 && TimeDifference >= 0);
        }

        [Fact]
        public void PeriodicEventDefaultValuesAreCorrect() {
            Configuration config = Configuration.Builder("SDK_KEY")
                .IsStreamingEnabled(false)
                .BaseUri(new Uri("http://fake"))
                .StartWaitTime(TimeSpan.Zero)
                .Build();
            IDiagnosticStore _serverDiagnosticStore = new ServerDiagnosticStore(config);
            IReadOnlyDictionary<string, object> PeriodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);

            Assert.Equal("diagnostic", PeriodicEvent["kind"]);
            DiagnosticId Id = (DiagnosticId) PeriodicEvent["id"];
            Assert.Equal("DK_KEY", Id._sdkKeySuffix);
            long TimeDifference = Util.GetUnixTimestampMillis(DateTime.Now) - (long) PeriodicEvent["creationDate"];
            Assert.True(TimeDifference < 50 && TimeDifference >= 0);

            Assert.Equal(4L, PeriodicEvent["eventsInQueue"]);
            Assert.Equal(0L, PeriodicEvent["droppedEvents"]);
            Assert.Equal(0L, PeriodicEvent["deduplicatedUsers"]);
            List<Dictionary<string, object>> StreamInits = (List<Dictionary<string, object>>) PeriodicEvent["streamInits"];
            Assert.True(StreamInits.Count == 0);
        }
    }
}