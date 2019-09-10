using System.Collections.Generic;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class LdClientEventTest
    {
        private static readonly User user = User.WithKey("userkey");
        private IFeatureStore featureStore = TestUtils.InMemoryFeatureStore();
        private TestEventProcessor eventSink = new TestEventProcessor();
        private ILdClient client;

        public LdClientEventTest()
        {
            var config = Configuration.Builder("SDK_KEY")
                .FeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore))
                .EventProcessorFactory(TestUtils.SpecificEventProcessor(eventSink))
                .UpdateProcessorFactory(Components.NullUpdateProcessor)
                .Build();
            client = new LdClient(config);
        }

        [Fact]
        public void IdentifySendsEvent()
        {
            client.Identify(user);

            Assert.Equal(1, eventSink.Events.Count);
            var ie = Assert.IsType<IdentifyEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ie.User.Key);
        }

        [Fact]
        public void IdentifyWithNoUserSendsNoEvent()
        {
            client.Identify(null);

            Assert.Equal(0, eventSink.Events.Count);
        }

        [Fact]
        public void IdentifyWithNoUserKeySendsNoEvent()
        {
            client.Identify(User.WithKey(null));

            Assert.Equal(0, eventSink.Events.Count);
        }

        [Fact]
        public void IdentifyWithEmptyUserKeySendsNoEvent()
        {
            client.Identify(User.WithKey(""));

            Assert.Equal(0, eventSink.Events.Count);
        }

        [Fact]
        public void TrackSendsEventWithoutData()
        {
            client.Track("eventkey", user);

            Assert.Equal(1, eventSink.Events.Count);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.Key);
            Assert.Equal(ImmutableJsonValue.Null, ce.ImmutableJsonData);
        }

        [Fact]
        public void TrackSendsEventWithData()
        {
            var data = new JObject();
            data.Add("thing", new JValue("stuff"));
            client.Track("eventkey", user, ImmutableJsonValue.FromJToken(data));

            Assert.Equal(1, eventSink.Events.Count);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.Key);
            Assert.Equal(data, ce.ImmutableJsonData.AsJToken());
        }

        [Fact]
        public void TrackSendsEventWithStringData()
        {
#pragma warning disable 0618
            client.Track("eventkey", user, "thing");
#pragma warning restore 0618

            Assert.Equal(1, eventSink.Events.Count);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.Key);
            Assert.Equal(ImmutableJsonValue.Of("thing"), ce.ImmutableJsonData);
        }

        [Fact]
        public void TrackWithNoUserSendsNoEvent()
        {
            client.Track("eventkey", null);

            Assert.Equal(0, eventSink.Events.Count);
        }

        [Fact]
        public void TrackWithNullUserKeySendsNoEvent()
        {
            client.Track("eventkey", User.WithKey(null));

            Assert.Equal(0, eventSink.Events.Count);
        }

        [Fact]
        public void TrackWithEmptyUserKeySendsNoEvent()
        {
            client.Track("eventkey", User.WithKey(""));

            Assert.Equal(0, eventSink.Events.Count);
        }

        [Fact]
        public void BoolVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(true)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.BoolVariation("key", user, false);
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, ImmutableJsonValue.Of(true), ImmutableJsonValue.Of(false), null);
        }

        [Fact]
        public void BoolVariationSendsEventForUnknownFlag()
        {
            client.BoolVariation("key", user, false);
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", ImmutableJsonValue.Of(false), null);
        }

        [Fact]
        public void IntVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(2)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.IntVariation("key", user, 1);
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, ImmutableJsonValue.Of(2), ImmutableJsonValue.Of(1), null);
        }

        [Fact]
        public void IntVariationSendsEventForUnknownFlag()
        {
            client.IntVariation("key", user, 1);
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", ImmutableJsonValue.Of(1), null);
        }

        [Fact]
        public void FloatVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(2.5f)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.FloatVariation("key", user, 1.0f);
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, ImmutableJsonValue.Of(2.5f), ImmutableJsonValue.Of(1.0f), null);
        }

        [Fact]
        public void FloatVariationSendsEventForUnknownFlag()
        {
            client.FloatVariation("key", user, 1.0f);
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", ImmutableJsonValue.Of(1.0f), null);
        }

        [Fact]
        public void StringVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue("b")).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.StringVariation("key", user, "a");
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, ImmutableJsonValue.Of("b"), ImmutableJsonValue.Of("a"), null);
        }

        [Fact]
        public void StringVariationSendsEventForUnknownFlag()
        {
            client.StringVariation("key", user, "a");
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", ImmutableJsonValue.Of("a"), null);
        }

        [Fact]
        public void JsonVariationSendsEvent()
        {
            var data = ImmutableJsonValue.FromDictionary(new Dictionary<string, string> { { "thing", "stuff" } });
            var flag = new FeatureFlagBuilder("key").OffWithValue(data.InnerValue).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);
            var defaultVal = ImmutableJsonValue.Of(42);

            client.JsonVariation("key", user, defaultVal);
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, data, defaultVal, null);
        }

        [Fact]
        public void JsonVariationSendsEventForUnknownFlag()
        {
            var defaultVal = ImmutableJsonValue.Of(42);

            client.JsonVariation("key", user, defaultVal);
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", defaultVal, null);
        }

        [Fact]
        public void DeprecatedJsonVariationSendsEvent()
        {
            var data = new JObject();
            data.Add("thing", new JValue("stuff"));
            var flag = new FeatureFlagBuilder("key").OffWithValue(data).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);
            var defaultVal = new JValue(42);

#pragma warning disable 0618
            client.JsonVariation("key", user, defaultVal);
#pragma warning restore 0618
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, ImmutableJsonValue.FromJToken(data),
                ImmutableJsonValue.FromJToken(defaultVal), null);
        }

        [Fact]
        public void DeprecatedJsonVariationSendsEventForUnknownFlag()
        {
            var defaultVal = ImmutableJsonValue.Of(42);

#pragma warning disable 0618
            client.JsonVariation("key", user, defaultVal);
#pragma warning restore 0618
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", defaultVal, null);
        }

        [Fact]
        public void EventIsSentForExistingPrerequisiteFlag()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new List<Prerequisite> { new Prerequisite("feature1", 1) })
                .Fallthrough(new VariationOrRollout(0, null))
                .OffVariation(1)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .Fallthrough(new VariationOrRollout(1, null))
                .Variations(new List<JToken> { new JValue("nogo"), new JValue("go") })
                .Version(2)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f0);
            featureStore.Upsert(VersionedDataKind.Features, f1);

            client.StringVariation("feature0", user, "default");

            Assert.Equal(2, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], f1, ImmutableJsonValue.Of("go"), ImmutableJsonValue.Null, "feature0");
            CheckFeatureEvent(eventSink.Events[1], f0, ImmutableJsonValue.Of("fall"), ImmutableJsonValue.Of("default"), null);
        }
        
        [Fact]
        public void EventIsNotSentForUnknownPrerequisiteFlag()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new List<Prerequisite> { new Prerequisite("feature1", 1) })
                .Fallthrough(new VariationOrRollout(0, null))
                .OffVariation(1)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Version(1)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f0);

            client.StringVariation("feature0", user, "default");

            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], f0, ImmutableJsonValue.Of("off"), ImmutableJsonValue.Of("default"), null);
        }

        private void CheckFeatureEvent(Event e, FeatureFlag flag, ImmutableJsonValue value, ImmutableJsonValue defaultVal, string prereqOf)
        {
            var fe = Assert.IsType<FeatureRequestEvent>(e);
            Assert.Equal(flag.Key, fe.Key);
            Assert.Equal(user.Key, fe.User.Key);
            Assert.Equal(flag.Version, fe.Version);
            Assert.Equal(value, fe.ImmutableJsonValue);
            Assert.Equal(defaultVal, fe.ImmutableJsonDefault);
            Assert.Equal(prereqOf, fe.PrereqOf);
        }

        private void CheckUnknownFeatureEvent(Event e, string key, ImmutableJsonValue defaultVal, string prereqOf)
        {
            var fe = Assert.IsType<FeatureRequestEvent>(e);
            Assert.Equal(key, fe.Key);
            Assert.Equal(user.Key, fe.User.Key);
            Assert.Null(fe.Version);
            Assert.Equal(defaultVal, fe.ImmutableJsonValue);
            Assert.Equal(defaultVal, fe.ImmutableJsonDefault);
            Assert.Equal(prereqOf, fe.PrereqOf);
        }
    }
}
