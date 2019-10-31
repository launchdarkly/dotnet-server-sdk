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
            IReadOnlyDictionary<string, object> initEvent = _serverDiagnosticStore.InitEvent;
            Assert.Equal("diagnostic-init", initEvent["kind"]);
            DiagnosticId id = (DiagnosticId) initEvent["id"];
            Assert.Equal("DK_KEY", id._sdkKeySuffix);
            long TimeDifference = Util.GetUnixTimestampMillis(DateTime.Now) - (long) initEvent["creationDate"];
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
            IReadOnlyDictionary<string, object> periodicEvent = _serverDiagnosticStore.CreateEventAndReset(4);

            Assert.Equal("diagnostic", periodicEvent["kind"]);
            DiagnosticId id = (DiagnosticId) periodicEvent["id"];
            Assert.Equal("DK_KEY", id._sdkKeySuffix);
            long TimeDifference = Util.GetUnixTimestampMillis(DateTime.Now) - (long) periodicEvent["creationDate"];
            Assert.True(TimeDifference < 50 && TimeDifference >= 0);

            Assert.Equal(4L, periodicEvent["eventsInQueue"]);
            Assert.Equal(0L, periodicEvent["droppedEvents"]);
            Assert.Equal(0L, periodicEvent["deduplicatedUsers"]);
            List<Dictionary<string, object>> StreamInits = (List<Dictionary<string, object>>) periodicEvent["streamInits"];
            Assert.True(StreamInits.Count == 0);
        }
    }
}