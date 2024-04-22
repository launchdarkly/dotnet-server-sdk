using System;
using System.Net;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Subsystems;
using LaunchDarkly.TestHelpers;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.MockResponses;
using static LaunchDarkly.TestHelpers.JsonAssertions;
using static LaunchDarkly.TestHelpers.JsonTestValue;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientDiagnosticEventTest : BaseTest
    {
        private const string testWrapperName = "wrapper-name";
        private const string testWrapperVersion = "1.2.3";
        private static readonly JsonTestValue expectedSdk = JsonOf(LdValue.BuildObject()
            .Add("name", "dotnet-server-sdk")
            .Add("version", AssemblyVersions.GetAssemblyVersionStringForType(typeof(LdClient)))
            .Add("wrapperName", testWrapperName)
            .Add("wrapperVersion", testWrapperVersion)
            .Build().ToJsonString());
        internal static readonly TimeSpan testStartWaitTime = TimeSpan.FromMilliseconds(1);

        private MockEventSender testEventSender = new MockEventSender { FilterKind = EventDataKind.DiagnosticEvent };

        private IDataStore IrrelevantDataStore => new InMemoryDataStore();
        private IPersistentDataStore IrrelevantPersistentDataStore => new MockCoreSync();
        private IPersistentDataStoreAsync IrrelevantPersistentDataStoreAsync => new MockCoreAsync();

        public LdClientDiagnosticEventTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void NoDiagnosticInitEventIsSentIfOptedOut()
        {
            var config = BasicConfig()
                .DiagnosticOptOut(true)
                .Events(Components.SendEvents().EventSender(testEventSender))
                .Build();
            using (var client = new LdClient(config))
            {
                testEventSender.RequireNoPayloadSent(TimeSpan.FromMilliseconds(100));
            }
        }

        [Fact]
        public void DiagnosticInitEventIsSent()
        {
            var config = BasicConfig()
                .Events(Components.SendEvents().EventSender(testEventSender))
                .Http(
                    Components.HttpConfiguration().Wrapper(testWrapperName, testWrapperVersion)
                )
                .Build();
            using (var client = new LdClient(config))
            {
                var payload = testEventSender.RequirePayload();

                Assert.Equal(EventDataKind.DiagnosticEvent, payload.Kind);
                Assert.Equal(1, payload.EventCount);

                var data = JsonOf(payload.Data);
                AssertJsonEqual(JsonFromValue("diagnostic-init"), data.Property("kind"));
                AssertJsonEqual(JsonFromValue("dotnet"), data.RequiredProperty("platform").Property("name"));
                AssertJsonEqual(JsonFromValue(ServerDiagnosticStore.GetDotNetTargetFramework()),
                    data.RequiredProperty("platform").Property("dotNetTargetFramework"));
                AssertJsonEqual(expectedSdk, data.Property("sdk"));
                AssertJsonEqual(JsonFromValue("dk-key"), data.Property("id").Property("sdkKeySuffix"));

                data.RequiredProperty("creationDate");
            }
        }

        [Fact]
        public void DiagnosticPeriodicEventsAreSent()
        {
            var config = BasicConfig()
                .Events(Components.SendEvents()
                    .EventSender(testEventSender)
                    .DiagnosticRecordingIntervalNoMinimum(TimeSpan.FromMilliseconds(50)))
                .Build();
            using (var client = new LdClient(config))
            {
                var payload1 = testEventSender.RequirePayload();

                Assert.Equal(EventDataKind.DiagnosticEvent, payload1.Kind);
                Assert.Equal(1, payload1.EventCount);
                var data1 = LdValue.Parse(payload1.Data);
                Assert.Equal("diagnostic-init", data1.Get("kind").AsString);
                var timestamp1 = data1.Get("creationDate").AsLong;
                Assert.NotEqual(0, timestamp1);

                var payload2 = testEventSender.RequirePayload();

                Assert.Equal(EventDataKind.DiagnosticEvent, payload2.Kind);
                Assert.Equal(1, payload2.EventCount);
                var data2 = LdValue.Parse(payload2.Data);
                Assert.Equal("diagnostic", data2.Get("kind").AsString);
                var timestamp2 = data2.Get("creationDate").AsLong;
                Assert.InRange(timestamp2, timestamp1, timestamp1 + 1000);

                var payload3 = testEventSender.RequirePayload();

                Assert.Equal(EventDataKind.DiagnosticEvent, payload3.Kind);
                Assert.Equal(1, payload3.EventCount);
                var data3 = LdValue.Parse(payload3.Data);
                Assert.Equal("diagnostic", data3.Get("kind").AsString);
                var timestamp3 = data2.Get("creationDate").AsLong;
                Assert.InRange(timestamp3, timestamp2, timestamp1 + 1000);
            }
        }

        [Fact]
        public void ConfigDefaults()
        {
            // Note that in all of the test configurations where the streaming or polling data source
            // is enabled, we're setting a fake HTTP message handler so it doesn't try to do any real
            // HTTP requests that would fail and (depending on timing) disrupt the test.
            TestDiagnosticConfig(
                c => c.Http(Components.HttpConfiguration().MessageHandler(
                    StreamWithEmptyData.AsMessageHandler())),
                null,
                ExpectedConfigProps.Base()
                );
        }

        [Fact]
        public void CustomConfigGeneralProperties()
        {
            TestDiagnosticConfig(
                c => c.StartWaitTime(TimeSpan.FromMilliseconds(2)),
                null,
                ExpectedConfigProps.Base()
                    .Set("startWaitMillis", 2)
                );
        }

        [Fact]
        public void CustomConfigForStreaming()
        {
            TestDiagnosticConfig(
                c => c.DataSource(
                    Components.StreamingDataSource()
                        .InitialReconnectDelay(TimeSpan.FromSeconds(2))
                    ),
                null,
                ExpectedConfigProps.Base()
                    .Set("reconnectTimeMillis", 2000)
                );
        }

        [Fact]
        public void CustomConfigForPolling()
        {
            TestDiagnosticConfig(
                c => c.DataSource(Components.PollingDataSource())
                    .Http(Components.HttpConfiguration().MessageHandler(EmptyPollingResponse.AsMessageHandler())),
                null,
                ExpectedConfigProps.Base()
                    .WithPollingDefaults()
                );

            TestDiagnosticConfig(
                c => c.DataSource(
                    Components.PollingDataSource()
                        .PollInterval(TimeSpan.FromSeconds(45))
                    )
                    .Http(Components.HttpConfiguration().MessageHandler(EmptyPollingResponse.AsMessageHandler())),
                null,
                ExpectedConfigProps.Base()
                    .WithPollingDefaults()
                    .Set("pollingIntervalMillis", 45000)
                );
        }

        [Fact]
        public void CustomConfigForExternalUpdatesOnly()
        {
            TestDiagnosticConfig(
                c => c.DataSource(Components.ExternalUpdatesOnly),
                null,
                ExpectedConfigProps.Base()
                    .Set("usingRelayDaemon", true)
                    .Remove("reconnectTimeMillis")
                );
        }

        [Fact]
        public void CustomConfigForEvents()
        {
            TestDiagnosticConfig(
                null,
                e => e.AllAttributesPrivate(true)
                    .Capacity(333)
                    .DiagnosticRecordingInterval(TimeSpan.FromMinutes(32))
                    .FlushInterval(TimeSpan.FromMilliseconds(555))
                    .ContextKeysCapacity(444)
                    .ContextKeysFlushInterval(TimeSpan.FromMinutes(23)),
                ExpectedConfigProps.Base()
                    .Set("allAttributesPrivate", true)
                    .Set("customEventsURI", false)
                    .Set("diagnosticRecordingIntervalMillis", TimeSpan.FromMinutes(32).TotalMilliseconds)
                    .Set("eventsCapacity", 333)
                    .Set("eventsFlushIntervalMillis", 555)
                    .Set("userKeysCapacity", 444)
                    .Set("userKeysFlushIntervalMillis", TimeSpan.FromMinutes(23).TotalMilliseconds)
                );
        }

        [Fact]
        public void CustomConfigForHTTP()
        {
            TestDiagnosticConfig(
                c => c.Http(
                        Components.HttpConfiguration()
                            .ConnectTimeout(TimeSpan.FromMilliseconds(8888))
                            .ReadTimeout(TimeSpan.FromMilliseconds(9999))
                            .MessageHandler(StreamWithEmptyData.AsMessageHandler())
                    ),
                null,
                ExpectedConfigProps.Base()
                    .Set("connectTimeoutMillis", 8888)
                    .Set("socketTimeoutMillis", 9999)
                    .Set("usingProxy", false)
                    .Set("usingProxyAuthenticator", false)
                );

            var proxyUri = new Uri("http://fake");
            var proxy = new WebProxy(proxyUri);
            TestDiagnosticConfig(
                c => c.Http(
                        Components.HttpConfiguration()
                            .Proxy(proxy)
                            .MessageHandler(StreamWithEmptyData.AsMessageHandler())
                    ),
                null,
                ExpectedConfigProps.Base()
                    .Set("usingProxy", true)
                );

            var credentials = new CredentialCache();
            credentials.Add(proxyUri, "Basic", new NetworkCredential("user", "pass"));
            var proxyWithAuth = new WebProxy(proxyUri);
            proxyWithAuth.Credentials = credentials;
            TestDiagnosticConfig(
                c => c.Http(
                        Components.HttpConfiguration()
                            .Proxy(proxyWithAuth)
                            .MessageHandler(StreamWithEmptyData.AsMessageHandler())
                    ),
                null,
                ExpectedConfigProps.Base()
                    .Set("usingProxy", true)
                    .Set("usingProxyAuthenticator", true)
                );
        }

        [Fact]
        public void TestConfigForServiceEndpoints()
        {
            TestDiagnosticConfig(
                c => c.ServiceEndpoints(Components.ServiceEndpoints().RelayProxy("http://custom"))
                    .Http(Components.HttpConfiguration().MessageHandler(StreamWithEmptyData.AsMessageHandler())),
                null,
                ExpectedConfigProps.Base()
                    .Set("customBaseURI", false) // this is the polling base URI, not relevant in streaming mode
                    .Set("customStreamURI", true)
                    .Set("customEventsURI", true)
                );

            TestDiagnosticConfig(
                c => c.ServiceEndpoints(Components.ServiceEndpoints().RelayProxy("http://custom"))
                    .DataSource(Components.PollingDataSource())
                    .Http(Components.HttpConfiguration().MessageHandler(EmptyPollingResponse.AsMessageHandler())),
                null,
                ExpectedConfigProps.Base()
                    .WithPollingDefaults()
                    .Set("customBaseURI", true)
                    .Set("customEventsURI", true)
                );

            TestDiagnosticConfig(
                c => c.ServiceEndpoints(Components.ServiceEndpoints()
                        .Streaming("http://custom-streaming")
                        .Polling("http://custom-polling")
                        .Events("http://custom-events"))
                    .Http(Components.HttpConfiguration().MessageHandler(StreamWithEmptyData.AsMessageHandler())),
                null,
                ExpectedConfigProps.Base()
                    .Set("customBaseURI", false) // this is the polling base URI, not relevant in streaming mode
                    .Set("customStreamURI", true)
                    .Set("customEventsURI", true)
                );
        }

        [Fact]
        public void CustomConfigForCustomDataStore()
        {
            TestDiagnosticConfig(
                c => c.DataStore(IrrelevantDataStore.AsSingletonFactoryWithDiagnosticDescription(LdValue.Of("my-test-store"))),
                null,
                ExpectedConfigProps.Base()
                    .Set("dataStoreType", "my-test-store")
                );

            TestDiagnosticConfig(
                c => c.DataStore(IrrelevantDataStore.AsSingletonFactory()),
                null,
                ExpectedConfigProps.Base()
                    .Set("dataStoreType", "custom")
                );

            TestDiagnosticConfig(
                c => c.DataStore(IrrelevantDataStore.AsSingletonFactoryWithDiagnosticDescription(LdValue.Of(4))),
                null,
                ExpectedConfigProps.Base()
                    .Set("dataStoreType", "custom")
                );
        }

        [Fact]
        public void CustomConfigForPersistentDataStore()
        {
            TestDiagnosticConfig(
                c => c.DataStore(Components.PersistentDataStore(
                    IrrelevantPersistentDataStore.AsSingletonFactoryWithDiagnosticDescription(LdValue.Of("my-test-store")))),
                null,
                ExpectedConfigProps.Base()
                    .Set("dataStoreType", "my-test-store")
                );

            TestDiagnosticConfig(
                c => c.DataStore(Components.PersistentDataStore(
                    IrrelevantPersistentDataStoreAsync.AsSingletonFactoryWithDiagnosticDescription(LdValue.Of("my-test-store")))),
                null,
                ExpectedConfigProps.Base()
                    .Set("dataStoreType", "my-test-store")
                );

            TestDiagnosticConfig(
                c => c.DataStore(Components.PersistentDataStore(IrrelevantPersistentDataStore.AsSingletonFactory())),
                null,
                ExpectedConfigProps.Base()
                    .Set("dataStoreType", "custom")
                );

            TestDiagnosticConfig(
                c => c.DataStore(Components.PersistentDataStore(IrrelevantPersistentDataStoreAsync.AsSingletonFactory())),
                null,
                ExpectedConfigProps.Base()
                    .Set("dataStoreType", "custom")
                );

            TestDiagnosticConfig(
                c => c.DataStore(Components.PersistentDataStore(
                    IrrelevantPersistentDataStore.AsSingletonFactoryWithDiagnosticDescription(LdValue.Of(4)))),
                null,
                ExpectedConfigProps.Base()
                    .Set("dataStoreType", "custom")
                );

            TestDiagnosticConfig(
                c => c.DataStore(Components.PersistentDataStore(
                    IrrelevantPersistentDataStoreAsync.AsSingletonFactoryWithDiagnosticDescription(LdValue.Of(4)))),
                null,
                ExpectedConfigProps.Base()
                    .Set("dataStoreType", "custom")
                );
        }

        private void TestDiagnosticConfig(
            Func<ConfigurationBuilder, ConfigurationBuilder> modConfig,
            Func<EventProcessorBuilder, EventProcessorBuilder> modEvents,
            LdValue.ObjectBuilder expected
            )
        {
            var eventsBuilder = Components.SendEvents()
                .EventSender(testEventSender);
            modEvents?.Invoke(eventsBuilder);
            var configBuilder = BasicConfig()
                .DataSource(null)
                .Events(eventsBuilder)
                .Http(Components.HttpConfiguration().MessageHandler(Error401Response.AsMessageHandler()))
                .StartWaitTime(testStartWaitTime);
            configBuilder = modConfig is null ? configBuilder : modConfig(configBuilder);
            using (var client = new LdClient(configBuilder.Build()))
            {
                var payload = testEventSender.RequirePayload();

                Assert.Equal(EventDataKind.DiagnosticEvent, payload.Kind);
                Assert.Equal(1, payload.EventCount);

                var data = JsonOf(payload.Data);
                AssertJsonEqual(JsonFromValue("diagnostic-init"), data.Property("kind"));

                AssertJsonEqual(JsonOf(expected.Build().ToJsonString()), data.Property("configuration"));
            }
        }
    }

    static class ExpectedConfigProps
    {
        public static LdValue.ObjectBuilder Base() =>
            LdValue.BuildObject()
                .Add("allAttributesPrivate", false)
                .Add("connectTimeoutMillis", HttpConfigurationBuilder.DefaultConnectTimeout.TotalMilliseconds)
                .Add("customBaseURI", false)
                .Add("customEventsURI", false)
                .Add("customStreamURI", false)
                .Add("dataStoreType", "memory")
                .Add("diagnosticRecordingIntervalMillis", EventProcessorBuilder.DefaultDiagnosticRecordingInterval.TotalMilliseconds)
                .Add("eventsCapacity", EventProcessorBuilder.DefaultCapacity)
                .Add("eventsFlushIntervalMillis", EventProcessorBuilder.DefaultFlushInterval.TotalMilliseconds)
                .Add("reconnectTimeMillis", StreamingDataSourceBuilder.DefaultInitialReconnectDelay.TotalMilliseconds)
                .Add("socketTimeoutMillis", HttpConfigurationBuilder.DefaultReadTimeout.TotalMilliseconds)
                .Add("startWaitMillis", LdClientDiagnosticEventTest.testStartWaitTime.TotalMilliseconds)
                .Add("streamingDisabled", false)
                .Add("userKeysCapacity", EventProcessorBuilder.DefaultContextKeysCapacity)
                .Add("userKeysFlushIntervalMillis", EventProcessorBuilder.DefaultContextKeysFlushInterval.TotalMilliseconds)
                .Add("usingProxy", false)
                .Add("usingProxyAuthenticator", false)
                .Add("usingRelayDaemon", false);

        public static LdValue.ObjectBuilder WithPollingDefaults(this LdValue.ObjectBuilder b) =>
            b.Set("streamingDisabled", true)
                .Set("pollingIntervalMillis", PollingDataSourceBuilder.DefaultPollInterval.TotalMilliseconds)
                .Remove("customStreamURI")
                .Remove("reconnectTimeMillis");
    }
}
