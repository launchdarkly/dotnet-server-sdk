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
            Assert.Equal(LdValue.Null, ce.LdValueData);
            Assert.Null(ce.MetricValue);
        }

        [Fact]
        public void TrackSendsEventWithData()
        {
            var data = LdValue.Convert.String.ObjectFrom(new Dictionary<string, string> { { "thing", "stuff" } });
            client.Track("eventkey", user, data);

            Assert.Equal(1, eventSink.Events.Count);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.Key);
            Assert.Equal(data, ce.LdValueData);
        }

        [Fact]
        public void TrackSendsEventWithDataDeprecatedMethod()
        {
            var data = new JObject();
            data.Add("thing", new JValue("stuff"));
#pragma warning disable 0618
            client.Track("eventkey", data, user);
#pragma warning restore 0618

            Assert.Equal(1, eventSink.Events.Count);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.Key);
#pragma warning disable 0618
            Assert.Equal(data, ce.LdValueData.AsJToken());
#pragma warning restore 0618
            Assert.Null(ce.MetricValue);
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
            Assert.Equal(LdValue.Of("thing"), ce.LdValueData);
            Assert.Null(ce.MetricValue);
        }

        [Fact]
        public void TrackSendsEventWithWithMetricValue()
        {
            var data = LdValue.Convert.String.ObjectFrom(new Dictionary<string, string> { { "thing", "stuff" } });
            client.Track("eventkey", user, data, 1.5);

            Assert.Equal(1, eventSink.Events.Count);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.Key);
            Assert.Equal(data, ce.LdValueData);
            Assert.Equal(1.5, ce.MetricValue);
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
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.Of(true), LdValue.Of(false), null);
        }

        [Fact]
        public void BoolVariationSendsEventForUnknownFlag()
        {
            client.BoolVariation("key", user, false);
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", LdValue.Of(false), null);
        }

        [Fact]
        public void IntVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(2)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.IntVariation("key", user, 1);
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.Of(2), LdValue.Of(1), null);
        }

        [Fact]
        public void IntVariationSendsEventForUnknownFlag()
        {
            client.IntVariation("key", user, 1);
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", LdValue.Of(1), null);
        }

        [Fact]
        public void FloatVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue(2.5f)).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.FloatVariation("key", user, 1.0f);
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.Of(2.5f), LdValue.Of(1.0f), null);
        }

        [Fact]
        public void FloatVariationSendsEventForUnknownFlag()
        {
            client.FloatVariation("key", user, 1.0f);
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", LdValue.Of(1.0f), null);
        }

        [Fact]
        public void StringVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").OffWithValue(new JValue("b")).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.StringVariation("key", user, "a");
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.Of("b"), LdValue.Of("a"), null);
        }

        [Fact]
        public void StringVariationSendsEventForUnknownFlag()
        {
            client.StringVariation("key", user, "a");
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", LdValue.Of("a"), null);
        }

        [Fact]
        public void JsonVariationSendsEvent()
        {
            var data = LdValue.Convert.String.ObjectFrom(new Dictionary<string, string> { { "thing", "stuff" } });
            var flag = new FeatureFlagBuilder("key").OffWithValue(data.InnerValue).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);
            var defaultVal = LdValue.Of(42);

            client.JsonVariation("key", user, defaultVal);
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, data, defaultVal, null);
        }

        [Fact]
        public void JsonVariationSendsEventForUnknownFlag()
        {
            var defaultVal = LdValue.Of(42);

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
            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.FromJToken(data),
                LdValue.FromJToken(defaultVal), null);
#pragma warning restore 0618
        }

        [Fact]
        public void DeprecatedJsonVariationSendsEventForUnknownFlag()
        {
            var defaultVal = LdValue.Of(42);

#pragma warning disable 0618
            client.JsonVariation("key", user, defaultVal);
#pragma warning restore 0618
            Assert.Equal(1, eventSink.Events.Count);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", defaultVal, null);
        }

        [Fact]
        public void EventTrackingAndReasonCanBeForcedForRule()
        {
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("rule-id").Variation(1).Clauses(clause).TrackEvents(true).Build();
            var flag = new FeatureFlagBuilder("flag")
                .On(true)
                .Rules(rule)
                .OffVariation(0)
                .Variations(new JValue("off"), new JValue("on"))
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.StringVariation("flag", user, "default");

            // Note, we did not call StringVariationDetail and the flag is not tracked, but we should still get
            // tracking and a reason, because the rule-level trackEvents flag is on for the matched rule.

            Assert.Equal(1, eventSink.Events.Count);
            var e = Assert.IsType<FeatureRequestEvent>(eventSink.Events[0]);
            Assert.True(e.TrackEvents);
            Assert.Equal(EvaluationReason.RuleMatchReason(0, "rule-id"), e.Reason);
        }

        [Fact]
        public void EventTrackingAndReasonAreNotForcedIfFlagIsNotSetForMatchingRule()
        {
            var clause0 = ClauseBuilder.ShouldNotMatchUser(user);
            var clause1 = ClauseBuilder.ShouldMatchUser(user);
            var rule0 = new RuleBuilder().Id("id0").Variation(1).Clauses(clause0).TrackEvents(true).Build();
            var rule1 = new RuleBuilder().Id("id1").Variation(1).Clauses(clause1).TrackEvents(false).Build();
            var flag = new FeatureFlagBuilder("flag")
                .On(true)
                .Rules(rule0, rule1)
                .OffVariation(0)
                .Variations(new JValue("off"), new JValue("on"))
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.StringVariation("flag", user, "default");

            // It matched rule1, which has trackEvents: false, so we don't get the override behavior

            Assert.Equal(1, eventSink.Events.Count);
            var e = Assert.IsType<FeatureRequestEvent>(eventSink.Events[0]);
            Assert.False(e.TrackEvents);
            Assert.Null(e.Reason);
        }

        [Fact]
        public void EventTrackingAndReasonCanBeForcedForFallthrough()
        {
            var flag = new FeatureFlagBuilder("flag")
                .On(true)
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .TrackEventsFallthrough(true)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.StringVariation("flag", user, "default");

            // Note, we did not call stringVariationDetail and the flag is not tracked, but we should still get
            // tracking and a reason, because trackEventsFallthrough is on and the evaluation fell through.

            Assert.Equal(1, eventSink.Events.Count);
            var e = Assert.IsType<FeatureRequestEvent>(eventSink.Events[0]);
            Assert.True(e.TrackEvents);
            Assert.Equal(EvaluationReason.FallthroughReason, e.Reason);
        }

        [Fact]
        public void EventTrackingAndReasonAreNotForcedForFallthroughIfFlagIsNotSet()
        {
            var flag = new FeatureFlagBuilder("flag")
                .On(true)
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            client.StringVariation("flag", user, "default");

            Assert.Equal(1, eventSink.Events.Count);
            var e = Assert.IsType<FeatureRequestEvent>(eventSink.Events[0]);
            Assert.False(e.TrackEvents);
            Assert.Null(e.Reason);
        }

        [Fact]
        public void EventIsSentForExistingPrerequisiteFlag()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .Fallthrough(new VariationOrRollout(0, null))
                .OffVariation(1)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .Fallthrough(new VariationOrRollout(1, null))
                .Variations(new JValue("nogo"), new JValue("go"))
                .Version(2)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f0);
            featureStore.Upsert(VersionedDataKind.Features, f1);

            client.StringVariation("feature0", user, "default");

            Assert.Equal(2, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], f1, LdValue.Of("go"), LdValue.Null, "feature0");
            CheckFeatureEvent(eventSink.Events[1], f0, LdValue.Of("fall"), LdValue.Of("default"), null);
        }
        
        [Fact]
        public void EventIsSentWithDefaultValueForFlagThatEvaluatesToNull()
        {
            var flag = new FeatureFlagBuilder("feature")
                .On(false)
                .OffVariation(null)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Version(1)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);
            var defaultVal = "default";

            var result = client.StringVariation(flag.Key, user, defaultVal);
            Assert.Equal(defaultVal, result);

            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.Of(defaultVal), LdValue.Of(defaultVal), null);
        }

        [Fact]
        public void EventIsNotSentForUnknownPrerequisiteFlag()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .Fallthrough(new VariationOrRollout(0, null))
                .OffVariation(1)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Version(1)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f0);

            client.StringVariation("feature0", user, "default");

            Assert.Equal(1, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], f0, LdValue.Of("off"), LdValue.Of("default"), null);
        }

        private void CheckFeatureEvent(Event e, FeatureFlag flag, LdValue value, LdValue defaultVal, string prereqOf)
        {
            var fe = Assert.IsType<FeatureRequestEvent>(e);
            Assert.Equal(flag.Key, fe.Key);
            Assert.Equal(user.Key, fe.User.Key);
            Assert.Equal(flag.Version, fe.Version);
            Assert.Equal(value, fe.LdValue);
            Assert.Equal(defaultVal, fe.LdValueDefault);
            Assert.Equal(prereqOf, fe.PrereqOf);
        }

        private void CheckUnknownFeatureEvent(Event e, string key, LdValue defaultVal, string prereqOf)
        {
            var fe = Assert.IsType<FeatureRequestEvent>(e);
            Assert.Equal(key, fe.Key);
            Assert.Equal(user.Key, fe.User.Key);
            Assert.Null(fe.Version);
            Assert.Equal(defaultVal, fe.LdValue);
            Assert.Equal(defaultVal, fe.LdValueDefault);
            Assert.Equal(prereqOf, fe.PrereqOf);
        }
    }
}
