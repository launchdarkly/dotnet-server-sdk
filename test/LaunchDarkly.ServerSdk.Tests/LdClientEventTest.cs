using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.EventProcessorTypes;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientEventTest : BaseTest
    {
        private static readonly User user = User.WithKey("userkey");
        private readonly TestData testData = TestData.DataSource();
        private readonly MockEventProcessor eventSink = new MockEventProcessor();
        private readonly ILdClient client;

        public LdClientEventTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            var config = BasicConfig()
                .DataSource(testData)
                .Events(eventSink.AsSingletonFactory())
                .Build();
            client = new LdClient(config);
        }

        [Fact]
        public void IdentifySendsEvent()
        {
            client.Identify(user);

            Assert.Single(eventSink.Events);
            var ie = Assert.IsType<IdentifyEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ie.User.Key);
        }

        [Fact]
        public void IdentifyWithNoUserSendsNoEvent()
        {
            client.Identify(null);

            Assert.Empty(eventSink.Events);
        }

        [Fact]
        public void IdentifyWithNoUserKeySendsNoEvent()
        {
            client.Identify(User.WithKey(null));

            Assert.Empty(eventSink.Events);
        }

        [Fact]
        public void IdentifyWithEmptyUserKeySendsNoEvent()
        {
            client.Identify(User.WithKey(""));

            Assert.Empty(eventSink.Events);
        }

        [Fact]
        public void TrackSendsEventWithoutData()
        {
            client.Track("eventkey", user);

            Assert.Single(eventSink.Events);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.EventKey);
            Assert.Equal(LdValue.Null, ce.Data);
            Assert.Null(ce.MetricValue);
        }

        [Fact]
        public void TrackSendsEventWithData()
        {
            var data = LdValue.BuildObject().Add("thing", "stuff").Build();
            client.Track("eventkey", user, data);

            Assert.Single(eventSink.Events);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.EventKey);
            Assert.Equal(data, ce.Data);
        }
        
        [Fact]
        public void TrackSendsEventWithWithMetricValue()
        {
            var data = LdValue.BuildObject().Add("thing", "stuff").Build();
            client.Track("eventkey", user, data, 1.5);

            Assert.Single(eventSink.Events);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(user.Key, ce.User.Key);
            Assert.Equal("eventkey", ce.EventKey);
            Assert.Equal(data, ce.Data);
            Assert.Equal(1.5, ce.MetricValue);
        }

        [Fact]
        public void TrackWithNoUserSendsNoEvent()
        {
            client.Track("eventkey", null);

            Assert.Empty(eventSink.Events);
        }

        [Fact]
        public void TrackWithNullUserKeySendsNoEvent()
        {
            client.Track("eventkey", User.WithKey(null));

            Assert.Empty(eventSink.Events);
        }

        [Fact]
        public void TrackWithEmptyUserKeySendsNoEvent()
        {
            client.Track("eventkey", User.WithKey(""));

            Assert.Empty(eventSink.Events);
        }

        [Fact]
        public void BoolVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").Version(1).OffWithValue(LdValue.Of(true)).Build();
            testData.UsePreconfiguredFlag(flag);

            client.BoolVariation("key", user, false);
            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.Of(true), LdValue.Of(false), null);
        }

        [Fact]
        public void BoolVariationSendsEventForUnknownFlag()
        {
            client.BoolVariation("key", user, false);
            Assert.Single(eventSink.Events);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", LdValue.Of(false), null);
        }

        [Fact]
        public void IntVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").Version(1).OffWithValue(LdValue.Of(2)).Build();
            testData.UsePreconfiguredFlag(flag);

            client.IntVariation("key", user, 1);
            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.Of(2), LdValue.Of(1), null);
        }

        [Fact]
        public void IntVariationSendsEventForUnknownFlag()
        {
            client.IntVariation("key", user, 1);
            Assert.Single(eventSink.Events);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", LdValue.Of(1), null);
        }

        [Fact]
        public void FloatVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").Version(1).OffWithValue(LdValue.Of(2.5f)).Build();
            testData.UsePreconfiguredFlag(flag);

            client.FloatVariation("key", user, 1.0f);
            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.Of(2.5f), LdValue.Of(1.0f), null);
        }

        [Fact]
        public void FloatVariationSendsEventForUnknownFlag()
        {
            client.FloatVariation("key", user, 1.0f);
            Assert.Single(eventSink.Events);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", LdValue.Of(1.0f), null);
        }

        [Fact]
        public void StringVariationSendsEvent()
        {
            var flag = new FeatureFlagBuilder("key").Version(1).OffWithValue(LdValue.Of("b")).Build();
            testData.UsePreconfiguredFlag(flag);

            client.StringVariation("key", user, "a");
            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.Of("b"), LdValue.Of("a"), null);
        }

        [Fact]
        public void StringVariationSendsEventForUnknownFlag()
        {
            client.StringVariation("key", user, "a");
            Assert.Single(eventSink.Events);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", LdValue.Of("a"), null);
        }

        [Fact]
        public void JsonVariationSendsEvent()
        {
            var data = LdValue.BuildObject().Add("thing", "stuff").Build();
            var flag = new FeatureFlagBuilder("key").Version(1).OffWithValue(data).Build();
            testData.UsePreconfiguredFlag(flag);
            var defaultVal = LdValue.Of(42);

            client.JsonVariation("key", user, defaultVal);
            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, data, defaultVal, null);
        }

        [Fact]
        public void JsonVariationSendsEventForUnknownFlag()
        {
            var defaultVal = LdValue.Of(42);

            client.JsonVariation("key", user, defaultVal);
            Assert.Single(eventSink.Events);
            CheckUnknownFeatureEvent(eventSink.Events[0], "key", defaultVal, null);
        }
        
        [Fact]
        public void EventTrackingAndReasonCanBeForcedForRule()
        {
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("rule-id").Variation(1).Clauses(clause).TrackEvents(true).Build();
            var flag = new FeatureFlagBuilder("flag").Version(1)
                .On(true)
                .Rules(rule)
                .OffVariation(0)
                .Variations(LdValue.Of("off"), LdValue.Of("on"))
                .Build();
            testData.UsePreconfiguredFlag(flag);

            client.StringVariation("flag", user, "default");

            // Note, we did not call StringVariationDetail and the flag is not tracked, but we should still get
            // tracking and a reason, because the rule-level trackEvents flag is on for the matched rule.

            Assert.Single(eventSink.Events);
            var e = Assert.IsType<EvaluationEvent>(eventSink.Events[0]);
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
            var flag = new FeatureFlagBuilder("flag").Version(1)
                .On(true)
                .Rules(rule0, rule1)
                .OffVariation(0)
                .Variations(LdValue.Of("off"), LdValue.Of("on"))
                .Build();
            testData.UsePreconfiguredFlag(flag);

            client.StringVariation("flag", user, "default");

            // It matched rule1, which has trackEvents: false, so we don't get the override behavior

            Assert.Single(eventSink.Events);
            var e = Assert.IsType<EvaluationEvent>(eventSink.Events[0]);
            Assert.False(e.TrackEvents);
            Assert.Null(e.Reason);
        }

        [Fact]
        public void EventTrackingAndReasonCanBeForcedForFallthrough()
        {
            var flag = new FeatureFlagBuilder("flag").Version(1)
                .On(true)
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(LdValue.Of("fall"), LdValue.Of("off"), LdValue.Of("on"))
                .TrackEventsFallthrough(true)
                .Build();
            testData.UsePreconfiguredFlag(flag);

            client.StringVariation("flag", user, "default");

            // Note, we did not call stringVariationDetail and the flag is not tracked, but we should still get
            // tracking and a reason, because trackEventsFallthrough is on and the evaluation fell through.

            Assert.Single(eventSink.Events);
            var e = Assert.IsType<EvaluationEvent>(eventSink.Events[0]);
            Assert.True(e.TrackEvents);
            Assert.Equal(EvaluationReason.FallthroughReason, e.Reason);
        }

        [Fact]
        public void EventTrackingAndReasonAreNotForcedForFallthroughIfFlagIsNotSet()
        {
            var flag = new FeatureFlagBuilder("flag").Version(1)
                .On(true)
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(LdValue.Of("fall"), LdValue.Of("off"), LdValue.Of("on"))
                .Build();
            testData.UsePreconfiguredFlag(flag);

            client.StringVariation("flag", user, "default");

            Assert.Single(eventSink.Events);
            var e = Assert.IsType<EvaluationEvent>(eventSink.Events[0]);
            Assert.False(e.TrackEvents);
            Assert.Null(e.Reason);
        }

        [Fact]
        public void EventIsSentForExistingPrerequisiteFlag()
        {
            var f0 = new FeatureFlagBuilder("feature0").Version(1)
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .Fallthrough(new VariationOrRollout(0, null))
                .OffVariation(1)
                .Variations(LdValue.Of("fall"), LdValue.Of("off"), LdValue.Of("on"))
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1").Version(1)
                .On(true)
                .Fallthrough(new VariationOrRollout(1, null))
                .Variations(LdValue.Of("nogo"), LdValue.Of("go"))
                .Version(2)
                .Build();
            testData.UsePreconfiguredFlag(f0);
            testData.UsePreconfiguredFlag(f1);

            client.StringVariation("feature0", user, "default");

            Assert.Equal(2, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], f1, LdValue.Of("go"), LdValue.Null, "feature0");
            CheckFeatureEvent(eventSink.Events[1], f0, LdValue.Of("fall"), LdValue.Of("default"), null);
        }
        
        [Fact]
        public void EventIsSentWithDefaultValueForFlagThatEvaluatesToNull()
        {
            var flag = new FeatureFlagBuilder("feature").Version(1)
                .On(false)
                .OffVariation(null)
                .Variations(LdValue.Of("fall"), LdValue.Of("off"), LdValue.Of("on"))
                .Version(1)
                .Build();
            testData.UsePreconfiguredFlag(flag);
            var defaultVal = "default";

            var result = client.StringVariation(flag.Key, user, defaultVal);
            Assert.Equal(defaultVal, result);

            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, LdValue.Of(defaultVal), LdValue.Of(defaultVal), null);
        }

        [Fact]
        public void EventIsNotSentForUnknownPrerequisiteFlag()
        {
            var f0 = new FeatureFlagBuilder("feature0").Version(1)
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .Fallthrough(new VariationOrRollout(0, null))
                .OffVariation(1)
                .Variations(LdValue.Of("fall"), LdValue.Of("off"), LdValue.Of("on"))
                .Version(1)
                .Build();
            testData.UsePreconfiguredFlag(f0);

            client.StringVariation("feature0", user, "default");

            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], f0, LdValue.Of("off"), LdValue.Of("default"), null);
        }

        [Fact]
        public void AliasSendsEvent()
        {
            client.Alias(User.WithKey("current"), User.Builder("previous").Anonymous(true).Build());

            Assert.Single(eventSink.Events);
            var e = Assert.IsType<AliasEvent>(eventSink.Events[0]);
            Assert.Equal("current", e.CurrentKey);
            Assert.Equal(ContextKind.User, e.CurrentKind);
            Assert.Equal("previous", e.PreviousKey);
            Assert.Equal(ContextKind.AnonymousUser, e.PreviousKind);
        }

        [Fact]
        public void AliasWithEmptyUserKeySendsNoEvent()
        {
            client.Alias(User.WithKey(""), User.WithKey("previous"));
            client.Alias(User.WithKey("current"), User.WithKey(""));

            Assert.Empty(eventSink.Events);
        }

        private void CheckFeatureEvent(object e, FeatureFlag flag, LdValue value, LdValue defaultVal, string prereqOf)
        {
            var fe = Assert.IsType<EvaluationEvent>(e);
            Assert.Equal(flag.Key, fe.FlagKey);
            Assert.Equal(user.Key, fe.User.Key);
            Assert.Equal(flag.Version, fe.FlagVersion);
            Assert.Equal(value, fe.Value);
            Assert.Equal(defaultVal, fe.Default);
            Assert.Equal(prereqOf, fe.PrerequisiteOf);
        }

        private void CheckUnknownFeatureEvent(object e, string key, LdValue defaultVal, string prereqOf)
        {
            var fe = Assert.IsType<EvaluationEvent>(e);
            Assert.Equal(key, fe.FlagKey);
            Assert.Equal(user.Key, fe.User.Key);
            Assert.Null(fe.FlagVersion);
            Assert.Equal(defaultVal, fe.Value);
            Assert.Equal(defaultVal, fe.Default);
            Assert.Equal(prereqOf, fe.PrerequisiteOf);
        }
    }
}
