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

        [Fact]
        public void InitEventFieldsAreCorrect() {
            Configuration config = Configuration.Builder("SDK_KEY")
                .IsStreamingEnabled(false)
                .BaseUri(new Uri("http://fake"))
                .StartWaitTime(TimeSpan.Zero)
                .WrapperName("Xamarin")
                .WrapperVersion("1.0.0")
                .Build();
            IDiagnosticStore _serverDiagnosticStore = new ServerDiagnosticStore(config);
            IReadOnlyDictionary<string, object> initEvent = _serverDiagnosticStore.InitEvent;
            Assert.Equal("diagnostic-init", initEvent["kind"]);
            DiagnosticId id = (DiagnosticId) initEvent["id"];
            Assert.Equal("DK_KEY", id._sdkKeySuffix);
            long TimeDifference = Util.GetUnixTimestampMillis(DateTime.Now) - (long) initEvent["creationDate"];
            Assert.True(TimeDifference < 50 && TimeDifference >= 0);

            Assert.Equal(_expectedPlatform, initEvent["platform"]);
            Assert.Equal(_expectedSdk, initEvent["sdk"]);
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