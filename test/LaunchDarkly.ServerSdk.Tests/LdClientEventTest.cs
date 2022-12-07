using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.Sdk.Server.Subsystems;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Subsystems.EventProcessorTypes;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientEventTest : BaseTest
    {
        private static readonly Context context = Context.New("userkey");
        private static readonly User contextAsUser = User.WithKey(context.Key);
        private static readonly Context invalidContext = Context.New("");

        private readonly TestData testData = TestData.DataSource();
        private readonly MockEventProcessor eventSink = new MockEventProcessor();
        private readonly ILdClient client;

        public LdClientEventTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            var config = BasicConfig()
                .DataSource(testData)
                .Events(eventSink.AsSingletonFactory<IEventProcessor>())
                .Build();
            client = new LdClient(config);
        }

        [Fact]
        public void IdentifySendsEvent()
        {
            client.Identify(context);

            Assert.Single(eventSink.Events);
            var ie = Assert.IsType<IdentifyEvent>(eventSink.Events[0]);
            Assert.Equal(context, ie.Context);
        }

        [Fact]
        public void IdentifyWithUserSendsEvent()
        {
            client.Identify(contextAsUser);

            Assert.Single(eventSink.Events);
            var ie = Assert.IsType<IdentifyEvent>(eventSink.Events[0]);
            Assert.Equal(context, ie.Context);
        }

        [Fact]
        public void IdentifyWithEmptyUserKeySendsNoEvent()
        {
            client.Identify(Context.New(""));

            Assert.Empty(eventSink.Events);
        }

        [Fact]
        public void TrackSendsEventWithoutData()
        {
            client.Track("eventkey", context);

            Assert.Single(eventSink.Events);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(context, ce.Context);
            Assert.Equal("eventkey", ce.EventKey);
            Assert.Equal(LdValue.Null, ce.Data);
            Assert.Null(ce.MetricValue);
        }

        [Fact]
        public void TrackWithUserSendsEventWithoutData()
        {
            client.Track("eventkey", contextAsUser);

            Assert.Single(eventSink.Events);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(context, ce.Context);
            Assert.Equal("eventkey", ce.EventKey);
            Assert.Equal(LdValue.Null, ce.Data);
            Assert.Null(ce.MetricValue);
        }

        [Fact]
        public void TrackSendsEventWithData()
        {
            var data = LdValue.BuildObject().Add("thing", "stuff").Build();
            client.Track("eventkey", context, data);

            Assert.Single(eventSink.Events);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(context, ce.Context);
            Assert.Equal("eventkey", ce.EventKey);
            Assert.Equal(data, ce.Data);
        }
        
        [Fact]
        public void TrackSendsEventWithWithMetricValue()
        {
            var data = LdValue.BuildObject().Add("thing", "stuff").Build();
            client.Track("eventkey", context, data, 1.5);

            Assert.Single(eventSink.Events);
            var ce = Assert.IsType<CustomEvent>(eventSink.Events[0]);
            Assert.Equal(context, ce.Context);
            Assert.Equal("eventkey", ce.EventKey);
            Assert.Equal(data, ce.Data);
            Assert.Equal(1.5, ce.MetricValue);
        }

        [Fact]
        public void TrackWithNullUserKeySendsNoEvent()
        {
            client.Track("eventkey", Context.New(null));

            Assert.Empty(eventSink.Events);
        }

        [Fact]
        public void TrackWithEmptyUserKeySendsNoEvent()
        {
            client.Track("eventkey", Context.New(""));

            Assert.Empty(eventSink.Events);
        }

        private void DoTypedEvaluationEventTests<T>(VariationMethodsDesc<T> v)
        {
            var flag = new FeatureFlagBuilder("flagkey").Version(2).OffWithValue(v.ExpectedLdValue).Build();
            testData.UsePreconfiguredFlag(flag);

            TypedEvaluationSendsEvent(flag, v);

            TypedEvaluationSendsEventForUnknownFlag("unknownflag", v);

            TypedEvaluationSendsNoEventForInvalidContext(flag, v);
        }

        private void TypedEvaluationSendsEvent<T>(FeatureFlag flag, VariationMethodsDesc<T> v)
        {
            v.VariationMethod(client, flag.Key, context, v.DefaultValue);
            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, context, v.ExpectedLdValue, v.DefaultLdValue, null, null);
            eventSink.Events.Clear();

            v.VariationForUserMethod(client, flag.Key, contextAsUser, v.DefaultValue);
            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, context, v.ExpectedLdValue, v.DefaultLdValue, null, null);
            eventSink.Events.Clear();

            v.VariationDetailMethod(client, flag.Key, context, v.DefaultValue);
            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, context, v.ExpectedLdValue, v.DefaultLdValue,
                EvaluationReason.OffReason, null);
            eventSink.Events.Clear();

            v.VariationDetailForUserMethod(client, flag.Key, contextAsUser, v.DefaultValue);
            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, context, v.ExpectedLdValue, v.DefaultLdValue,
                EvaluationReason.OffReason, null);
            eventSink.Events.Clear();
        }

        private void TypedEvaluationSendsEventForUnknownFlag<T>(string flagKey, VariationMethodsDesc<T> v)
        {
            v.VariationMethod(client, flagKey, context, v.DefaultValue);
            Assert.Single(eventSink.Events);
            CheckUnknownFeatureEvent(eventSink.Events[0], flagKey, context, v.DefaultLdValue, null, null);
            eventSink.Events.Clear();

            v.VariationForUserMethod(client, flagKey, contextAsUser, v.DefaultValue);
            Assert.Single(eventSink.Events);
            CheckUnknownFeatureEvent(eventSink.Events[0], flagKey, context, v.DefaultLdValue, null, null);
            eventSink.Events.Clear();

            v.VariationDetailMethod(client, flagKey, context, v.DefaultValue);
            Assert.Single(eventSink.Events);
            CheckUnknownFeatureEvent(eventSink.Events[0], flagKey, context, v.DefaultLdValue,
                EvaluationReason.ErrorReason(EvaluationErrorKind.FlagNotFound), null);
            eventSink.Events.Clear();

            v.VariationDetailForUserMethod(client, flagKey, contextAsUser, v.DefaultValue);
            Assert.Single(eventSink.Events);
            CheckUnknownFeatureEvent(eventSink.Events[0], flagKey, context, v.DefaultLdValue,
                EvaluationReason.ErrorReason(EvaluationErrorKind.FlagNotFound), null);
            eventSink.Events.Clear();
        }

        private void TypedEvaluationSendsNoEventForInvalidContext<T>(FeatureFlag flag, VariationMethodsDesc<T> v)
        {
            v.VariationMethod(client, flag.Key, invalidContext, v.DefaultValue);
            Assert.Empty(eventSink.Events);

            v.VariationForUserMethod(client, flag.Key, (User)null, v.DefaultValue);
            Assert.Empty(eventSink.Events);

            v.VariationDetailMethod(client, flag.Key, invalidContext, v.DefaultValue);
            Assert.Empty(eventSink.Events);

            v.VariationDetailForUserMethod(client, flag.Key, (User)null, v.DefaultValue);
            Assert.Empty(eventSink.Events);
        }

        [Fact]
        public void BoolVariationEventTests() =>
            DoTypedEvaluationEventTests(VariationMethodsDesc.Bool);

        [Fact]
        public void IntVariationEventTests() =>
            DoTypedEvaluationEventTests(VariationMethodsDesc.Int);

        [Fact]
        public void FloatVariationEventTests() =>
            DoTypedEvaluationEventTests(VariationMethodsDesc.Float);

        [Fact]
        public void DoubleVariationEventTests() =>
            DoTypedEvaluationEventTests(VariationMethodsDesc.Double);

        [Fact]
        public void StringVariationEventTests() =>
            DoTypedEvaluationEventTests(VariationMethodsDesc.String);

        [Fact]
        public void JsonVariationEventTests() =>
            DoTypedEvaluationEventTests(VariationMethodsDesc.Json);

        [Fact]
        public void EventTrackingAndReasonCanBeForcedForRule()
        {
            var clause = ClauseBuilder.ShouldMatchUser(context);
            var rule = new RuleBuilder().Id("rule-id").Variation(1).Clauses(clause).TrackEvents(true).Build();
            var flag = new FeatureFlagBuilder("flag").Version(1)
                .On(true)
                .Rules(rule)
                .OffVariation(0)
                .Variations("off", "on")
                .Build();
            testData.UsePreconfiguredFlag(flag);

            client.StringVariation("flag", context, "default");

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
            var clause0 = ClauseBuilder.ShouldNotMatchUser(context);
            var clause1 = ClauseBuilder.ShouldMatchUser(context);
            var rule0 = new RuleBuilder().Id("id0").Variation(1).Clauses(clause0).TrackEvents(true).Build();
            var rule1 = new RuleBuilder().Id("id1").Variation(1).Clauses(clause1).TrackEvents(false).Build();
            var flag = new FeatureFlagBuilder("flag").Version(1)
                .On(true)
                .Rules(rule0, rule1)
                .OffVariation(0)
                .Variations("off", "on")
                .Build();
            testData.UsePreconfiguredFlag(flag);

            client.StringVariation("flag", context, "default");

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
                .Variations("fall", "off", "on")
                .TrackEventsFallthrough(true)
                .Build();
            testData.UsePreconfiguredFlag(flag);

            client.StringVariation("flag", context, "default");

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
                .Variations("fall", "off", "on")
                .Build();
            testData.UsePreconfiguredFlag(flag);

            client.StringVariation("flag", context, "default");

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
                .Variations("fall", "off", "on")
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1").Version(1)
                .On(true)
                .Fallthrough(new VariationOrRollout(1, null))
                .Variations("nogo", "go")
                .Version(2)
                .Build();
            testData.UsePreconfiguredFlag(f0);
            testData.UsePreconfiguredFlag(f1);

            client.StringVariation("feature0", context, "default");

            Assert.Equal(2, eventSink.Events.Count);
            CheckFeatureEvent(eventSink.Events[0], f1, context, LdValue.Of("go"), LdValue.Null, null, "feature0");
            CheckFeatureEvent(eventSink.Events[1], f0, context, LdValue.Of("fall"), LdValue.Of("default"), null, null);
        }
        
        [Fact]
        public void EventIsSentWithDefaultValueForFlagThatEvaluatesToNull()
        {
            var flag = new FeatureFlagBuilder("feature").Version(1)
                .On(false)
                .OffVariation(null)
                .Variations("fall", "off", "on")
                .Version(1)
                .Build();
            testData.UsePreconfiguredFlag(flag);
            var defaultVal = "default";

            var result = client.StringVariation(flag.Key, context, defaultVal);
            Assert.Equal(defaultVal, result);

            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], flag, context, LdValue.Of(defaultVal), LdValue.Of(defaultVal), null, null);
        }

        [Fact]
        public void EventIsNotSentForUnknownPrerequisiteFlag()
        {
            var f0 = new FeatureFlagBuilder("feature0").Version(1)
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .Fallthrough(new VariationOrRollout(0, null))
                .OffVariation(1)
                .Variations("fall", "off", "on")
                .Version(1)
                .Build();
            testData.UsePreconfiguredFlag(f0);

            client.StringVariation("feature0", context, "default");

            Assert.Single(eventSink.Events);
            CheckFeatureEvent(eventSink.Events[0], f0, context, LdValue.Of("off"), LdValue.Of("default"), null, null);
        }

        private void CheckFeatureEvent(object e, FeatureFlag flag, Context context, LdValue value, LdValue defaultVal,
            EvaluationReason? reason, string prereqOf)
        {
            var fe = Assert.IsType<EvaluationEvent>(e);
            Assert.Equal(flag.Key, fe.FlagKey);
            Assert.Equal(context, fe.Context);
            Assert.Equal(flag.Version, fe.FlagVersion);
            Assert.Equal(value, fe.Value);
            Assert.Equal(defaultVal, fe.Default);
            Assert.Equal(reason, fe.Reason);
            Assert.Equal(prereqOf, fe.PrerequisiteOf);
        }

        private void CheckUnknownFeatureEvent(object e, string key, Context context, LdValue defaultVal, EvaluationReason? reason, string prereqOf)
        {
            var fe = Assert.IsType<EvaluationEvent>(e);
            Assert.Equal(key, fe.FlagKey);
            Assert.Equal(context, fe.Context);
            Assert.Null(fe.FlagVersion);
            Assert.Equal(defaultVal, fe.Value);
            Assert.Equal(defaultVal, fe.Default);
            Assert.Equal(reason, fe.Reason);
            Assert.Equal(prereqOf, fe.PrerequisiteOf);
        }
    }
}
