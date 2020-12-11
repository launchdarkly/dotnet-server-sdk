﻿using System;
using System.Net;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Integrations;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.TestHttpUtils;
using static LaunchDarkly.Sdk.Server.TestUtils;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientDiagnosticEventTest : BaseTest
    {
        private const string sdkKey = "SDK_KEY";
        private const string testWrapperName = "wrapper-name";
        private const string testWrapperVersion = "1.2.3";
        private static readonly LdValue expectedPlatform =
            LdValue.BuildObject().Add("name", "dotnet").Build();
        private static readonly LdValue expectedSdk = LdValue.BuildObject()
            .Add("name", "dotnet-server-sdk")
            .Add("version", AssemblyVersions.GetAssemblyVersionStringForType(typeof(LdClient)))
            .Add("wrapperName", testWrapperName)
            .Add("wrapperVersion", testWrapperVersion)
            .Build();
        internal static readonly TimeSpan testStartWaitTime = TimeSpan.FromMilliseconds(1);

        private TestEventSender testEventSender = new TestEventSender();

        public LdClientDiagnosticEventTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void NoDiagnosticInitEventIsSentIfOptedOut()
        {
            var config = Configuration.Builder(sdkKey)
                .DiagnosticOptOut(true)
                .DataSource(Components.ExternalUpdatesOnly)
                .Events(Components.SendEvents().EventSender(testEventSender))
                .Logging(Components.Logging(testLogging))
                .Build();
            using (var client = new LdClient(config))
            {
                testEventSender.RequireNoPayloadSent(TimeSpan.FromMilliseconds(100));
            }
        }

        [Fact]
        public void DiagnosticInitEventIsSent()
        {
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .Events(Components.SendEvents().EventSender(testEventSender))
                .Http(
                    Components.HttpConfiguration().Wrapper(testWrapperName, testWrapperVersion)
                )
                .Logging(Components.Logging(testLogging))
                .Build();
            using (var client = new LdClient(config))
            {
                var payload = testEventSender.RequirePayload();

                Assert.Equal(EventDataKind.DiagnosticEvent, payload.Kind);
                Assert.Equal(1, payload.EventCount);

                var data = LdValue.Parse(payload.Data);
                Assert.Equal("diagnostic-init", data.Get("kind").AsString);
                AssertJsonEqual(expectedPlatform, data.Get("platform"));
                AssertJsonEqual(expectedSdk, data.Get("sdk"));
                Assert.Equal("DK_KEY", data.Get("id").Get("sdkKeySuffix").AsString);

                var timestamp = data.Get("creationDate").AsLong;
                Assert.NotEqual(0, timestamp);
            }
        }

        [Fact]
        public void DiagnosticPeriodicEventsAreSent()
        {
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .Events(Components.SendEvents()
                    .EventSender(testEventSender)
                    .DiagnosticRecordingIntervalNoMinimum(TimeSpan.FromMilliseconds(50)))
                .Logging(Components.Logging(testLogging))
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
                c => c.Http(Components.HttpConfiguration().MessageHandler(StubMessageHandler.EmptyStreamingResponse())),
                null,
                ExpectedConfigProps.Base()
                    .WithEventsDefaults()
                    .WithStreamingDefaults()
                );
        }

        [Fact]
        public void CustomConfigGeneralProperties()
        {
            TestDiagnosticConfig(
                c => c.Http(
                        Components.HttpConfiguration()
                            .ConnectTimeout(TimeSpan.FromMilliseconds(1001))
                            .ReadTimeout(TimeSpan.FromMilliseconds(1003))
                            .MessageHandler(StubMessageHandler.EmptyStreamingResponse())
                    )
                    .StartWaitTime(TimeSpan.FromMilliseconds(2)),
                null,
                LdValue.BuildObject()
                    .WithEventsDefaults()
                    .WithStreamingDefaults()
                    .Add("connectTimeoutMillis", 1001)
                    .Add("socketTimeoutMillis", 1003)
                    .Add("startWaitMillis", 2)
                    .Add("usingProxy", false)
                    .Add("usingProxyAuthenticator", false)
                );
        }

        [Fact]
        public void CustomConfigForStreaming()
        {
            TestDiagnosticConfig(
                c => c.DataSource(
                    Components.StreamingDataSource()
                        .InitialReconnectDelay(TimeSpan.FromSeconds(2))
                    )
                    .Http(Components.HttpConfiguration().MessageHandler(StubMessageHandler.EmptyStreamingResponse())),
                null,
                ExpectedConfigProps.Base()
                    .WithEventsDefaults()
                    .Add("customBaseURI", false)
                    .Add("customStreamURI", false)
                    .Add("streamingDisabled", false)
                    .Add("reconnectTimeMillis", 2000)
                    .Add("usingRelayDaemon", false)
                );

            TestDiagnosticConfig(
                c => c.DataSource(
                    Components.StreamingDataSource()
                        .BaseUri(new Uri("http://custom"))
                        .InitialReconnectDelay(TimeSpan.FromSeconds(2))
                    )
                    .Http(Components.HttpConfiguration().MessageHandler(StubMessageHandler.EmptyStreamingResponse())),
                null,
                ExpectedConfigProps.Base()
                    .WithEventsDefaults()
                    .Add("customBaseURI", false)
                    .Add("customStreamURI", true)
                    .Add("streamingDisabled", false)
                    .Add("reconnectTimeMillis", 2000)
                    .Add("usingRelayDaemon", false)
                );
        }

        [Fact]
        public void CustomConfigForPolling()
        {
            TestDiagnosticConfig(
                c => c.DataSource(
                    Components.PollingDataSource()
                        .PollInterval(TimeSpan.FromSeconds(45))
                    )
                    .Http(Components.HttpConfiguration().MessageHandler(StubMessageHandler.EmptyPollingResponse())),
                null,
                ExpectedConfigProps.Base()
                    .WithEventsDefaults()
                    .Add("customBaseURI", false)
                    .Add("customStreamURI", false)
                    .Add("pollingIntervalMillis", 45000)
                    .Add("streamingDisabled", true)
                    .Add("usingRelayDaemon", false)
                );

            TestDiagnosticConfig(
                c => c.DataSource(
                    Components.PollingDataSource()
                        .BaseUri(new Uri("http://custom"))
                        .PollInterval(TimeSpan.FromSeconds(45))
                    )
                   .Http(Components.HttpConfiguration().MessageHandler(StubMessageHandler.EmptyPollingResponse())),
                null,
                ExpectedConfigProps.Base()
                    .WithEventsDefaults()
                    .Add("customBaseURI", true)
                    .Add("customStreamURI", false)
                    .Add("pollingIntervalMillis", 45000)
                    .Add("streamingDisabled", true)
                    .Add("usingRelayDaemon", false)
                );
        }

        [Fact]
        public void CustomConfigForExternalUpdatesOnly()
        {
            TestDiagnosticConfig(
                c => c.DataSource(Components.ExternalUpdatesOnly),
                null,
                ExpectedConfigProps.Base()
                    .WithEventsDefaults()
                    .Add("customBaseURI", false)
                    .Add("customStreamURI", false)
                    .Add("streamingDisabled", false)
                    .Add("usingRelayDaemon", true)
                );
        }

        [Fact]
        public void CustomConfigForEvents()
        {
            TestDiagnosticConfig(
                c => c.Http(Components.HttpConfiguration().MessageHandler(StubMessageHandler.EmptyStreamingResponse())),
                e => e.AllAttributesPrivate(true)
                    .BaseUri(new Uri("http://custom"))
                    .Capacity(333)
                    .DiagnosticRecordingInterval(TimeSpan.FromMinutes(32))
                    .FlushInterval(TimeSpan.FromMilliseconds(555))
                    .InlineUsersInEvents(true)
                    .UserKeysCapacity(444)
                    .UserKeysFlushInterval(TimeSpan.FromMinutes(23)),
                ExpectedConfigProps.Base()
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
            var configBuilder = Configuration.Builder(sdkKey)
                .Events(eventsBuilder)
                .Http(Components.HttpConfiguration().MessageHandler(new StubMessageHandler(HttpStatusCode.Unauthorized)))
                .Logging(Components.Logging(testLogging))
                .StartWaitTime(testStartWaitTime);
            modConfig?.Invoke(configBuilder);
            using (var client = new LdClient(configBuilder.Build()))
            {
                var payload = testEventSender.RequirePayload();

                Assert.Equal(EventDataKind.DiagnosticEvent, payload.Kind);
                Assert.Equal(1, payload.EventCount);

                var data = LdValue.Parse(payload.Data);
                Assert.Equal("diagnostic-init", data.Get("kind").AsString);

                AssertJsonEqual(expected.Build(), data.Get("configuration"));
            }
        }
    }

    static class ExpectedConfigProps
    {
        public static LdValue.ObjectBuilder Base() =>
            LdValue.BuildObject()
                .Add("connectTimeoutMillis", HttpConfigurationBuilder.DefaultConnectTimeout.TotalMilliseconds)
                .Add("socketTimeoutMillis", HttpConfigurationBuilder.DefaultReadTimeout.TotalMilliseconds)
                .Add("startWaitMillis", LdClientDiagnosticEventTest.testStartWaitTime.TotalMilliseconds)
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