using System.Collections.Generic;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class LdClientEventTest
    {
        private static readonly User user = User.WithKey("userkey");
        private IFeatureStore featureStore = new InMemoryFeatureStore();
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
            Assert.Null(ce.JsonData);
        }

        [Fact]
        public void TrackSendsEventWithData()
        {
            var data = new JObject();
            data.Add("thing", new JValue("stuff"));
            client.Track("eventkey", data, user);

            Assert.Equal(1, eventSink.Events.Count);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.Key);
            Assert.Equal(data, ce.JsonData);
        }

        [Fact]
        public void TrackSendsEventWithStringData()
        {
            client.Track("eventkey", user, "thing");

            Assert.Equal(1, eventSink.Events.Count);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.Key);
            Assert.Equal(new JValue("thing"), ce.JsonData);
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
            CheckFeatureEvent(eventSink.Events[0], flag, new JValue(true), new JValue(false), null);
        }

        [Fact]
        public void BoolVariationSendsEventForUnknownFlag()
        {
            client.BoolVariation("key", user, false);
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", new JValue(false), null);
        }

        [Fact]
        public void IntVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(2)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.IntVariation("key", user, 1);
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, new JValue(2), new JValue(1), null);
        }

        [Fact]
        public void IntVariationSendsEventForUnknownFlag()
        {
            client.IntVariation("key", user, 1);
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", new JValue(1), null);
        }

        [Fact]
        public void FloatVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(2.5f)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.FloatVariation("key", user, 1.0f);
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, new JValue(2.5f), new JValue(1.0f), null);
        }

        [Fact]
        public void FloatVariationSendsEventForUnknownFlag()
        {
            client.FloatVariation("key", user, 1.0f);
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", new JValue(1.0f), null);
        }

        [Fact]
        public void StringVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue("b")).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.StringVariation("key", user, "a");
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, new JValue("b"), new JValue("a"), null);
        }

        [Fact]
        public void StringVariationSendsEventForUnknownFlag()
        {
            client.StringVariation("key", user, "a");
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", new JValue("a"), null);
        }

        [Fact]
        public void JsonVariationSendsEvent()
        {
            var data = new JObject();
            data.Add("thing", new JValue("stuff"));
            var flag = new FeatureFlagBuilder("key").OffWithValue(data).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);
            var defaultVal = new JValue(42);

            client.JsonVariation("key", user, defaultVal);
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, data, defaultVal, null);
        }

        [Fact]
        public void JsonVariationSendsEventForUnknownFlag()
        {
            var defaultVal = new JValue(42);

            client.JsonVariation("key", user, defaultVal);
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
            CheckFeatureEvent(eventSink.Events[0], f1, new JValue("go"), null, "feature0");
            CheckFeatureEvent(eventSink.Events[1], f0, new JValue("fall"), new JValue("default"), null);
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
            CheckFeatureEvent(eventSink.Events[0], f0, new JValue("off"), new JValue("default"), null);
        }

        private void CheckFeatureEvent(Event e, FeatureFlag flag, JToken value, JToken defaultVal, string prereqOf)
        {
            var fe = Assert.IsType<FeatureRequestEvent>(e);
            Assert.Equal(flag.Key, fe.Key);
            Assert.Equal(user.Key, fe.User.Key);
            Assert.Equal(flag.Version, fe.Version);
            Assert.Equal(value, fe.Value);
            Assert.Equal(defaultVal, fe.Default);
            Assert.Equal(prereqOf, fe.PrereqOf);
        }

        private void CheckUnknownFeatureEvent(Event e, string key, JToken defaultVal, string prereqOf)
        {
            var fe = Assert.IsType<FeatureRequestEvent>(e);
            Assert.Equal(key, fe.Key);
            Assert.Equal(user.Key, fe.User.Key);
            Assert.Null(fe.Version);
            Assert.Equal(defaultVal, fe.Value);
            Assert.Equal(defaultVal, fe.Default);
            Assert.Equal(prereqOf, fe.PrereqOf);
        }
    }
}
