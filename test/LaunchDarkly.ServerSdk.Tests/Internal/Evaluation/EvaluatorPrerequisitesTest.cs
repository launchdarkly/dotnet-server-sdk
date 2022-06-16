using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    public class EvaluatorPrerequisitesTest
    {
        private static readonly Context baseUser = Context.New("userkey");
        private static readonly LdValue fallthroughValue = LdValue.Of("fallthrough");
        private static readonly LdValue offValue = LdValue.Of("off");
        private static readonly LdValue onValue = LdValue.Of("on");

        [Fact]
        public void FlagReturnsOffVariationIfPrerequisiteIsNotFound()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var evaluator = BasicEvaluator.WithNonexistentFlag("feature1");
            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(offValue, 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
        }

        [Fact]
        public void FlagReturnsOffVariationAndEventIfPrerequisiteIsOff()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(fallthroughValue, offValue, onValue)
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(false)
                .OffVariation(1)
                // note that even though it returns the desired variation, it is still off and therefore not a match
                .Variations("nogo", "go")
                .Version(2)
                .Build();
            var evaluator = BasicEvaluator.WithStoredFlags(f1);

            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(offValue, 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);

            Assert.Collection(result.PrerequisiteEvals,
                e =>
                {
                    Assert.Equal(f1.Key, e.PrerequisiteFlag.Key);
                    Assert.Equal(LdValue.Of("go"), e.Result.Value);
                    Assert.Equal(f1.Version, e.PrerequisiteFlag.Version);
                    Assert.Equal(f0.Key, e.PrerequisiteOfFlagKey);
                });
        }

        [Fact]
        public void FlagReturnsOffVariationAndEventIfPrerequisiteIsNotMet()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(fallthroughValue, offValue, onValue)
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .FallthroughVariation(0)
                .Variations("nogo", "go")
                .Version(2)
                .Build();
            var evaluator = BasicEvaluator.WithStoredFlags(f1);

            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(offValue, 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);

            Assert.Collection(result.PrerequisiteEvals,
                e =>
                {
                    Assert.Equal(f1.Key, e.PrerequisiteFlag.Key);
                    Assert.Equal(LdValue.Of("nogo"), e.Result.Value);
                    Assert.Equal(f1.Version, e.PrerequisiteFlag.Version);
                    Assert.Equal(f0.Key, e.PrerequisiteOfFlagKey);
                });
        }

        [Fact]
        public void FlagReturnsFallthroughVariationAndEventIfPrerequisiteIsMetAndThereAreNoRules()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(fallthroughValue, offValue, onValue)
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .FallthroughVariation(1) // this is what makes the prerequisite pass
                .Variations("nogo", "go")
                .Version(2)
                .Build();
            var evaluator = BasicEvaluator.WithStoredFlags(f1);

            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(fallthroughValue, 0, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result.Result);

            Assert.Collection(result.PrerequisiteEvals,
                e =>
                {
                    Assert.Equal(f1.Key, e.PrerequisiteFlag.Key);
                    Assert.Equal(LdValue.Of("go"), e.Result.Value);
                    Assert.Equal(f1.Version, e.PrerequisiteFlag.Version);
                    Assert.Equal(f0.Key, e.PrerequisiteOfFlagKey);
                });
        }

        [Fact]
        public void MultipleLevelsOfPrerequisitesProduceMultipleEvents()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(fallthroughValue, offValue, onValue)
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .Prerequisites(new Prerequisite("feature2", 1))
                .FallthroughVariation(1)
                .Variations("nogo", "go")
                .Version(2)
                .Build();
            var f2 = new FeatureFlagBuilder("feature2")
                .On(true)
                .FallthroughVariation(1)
                .Variations("nogo", "go")
                .Version(3)
                .Build();
            var evaluator = BasicEvaluator.WithStoredFlags(f1, f2);

            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(fallthroughValue, 0, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result.Result);

            Assert.Equal(2, result.PrerequisiteEvals.Count);

            Assert.Collection(result.PrerequisiteEvals,
                e =>
                {
                    Assert.Equal(f2.Key, e.PrerequisiteFlag.Key);
                    Assert.Equal(LdValue.Of("go"), e.Result.Value);
                    Assert.Equal(f2.Version, e.PrerequisiteFlag.Version);
                    Assert.Equal(f1.Key, e.PrerequisiteOfFlagKey);
                },
                e =>
                {
                    Assert.Equal(f1.Key, e.PrerequisiteFlag.Key);
                    Assert.Equal(LdValue.Of("go"), e.Result.Value);
                    Assert.Equal(f1.Version, e.PrerequisiteFlag.Version);
                    Assert.Equal(f0.Key, e.PrerequisiteOfFlagKey);
                });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PrerequisiteCycleDetection(bool cycleGoesToOriginalFlag)
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .Variations(fallthroughValue, offValue, onValue)
                .On(true).OffVariation(1)
                .FallthroughVariation(0)
                .Prerequisites(new Prerequisite("feature1", 1))
                .Build();

            var f1 = new FeatureFlagBuilder("feature1")
                .Variations(fallthroughValue, offValue, onValue)
                .On(true).OffVariation(1)
                .FallthroughVariation(1) // this 1 matches the 1 in f0's prerequisites
                .Prerequisites(new Prerequisite("feature2", 1))
                .Build();

            var cycleTargetKey = cycleGoesToOriginalFlag ? f0.Key : f1.Key;
            var f2 = new FeatureFlagBuilder("feature2")
                .Variations("nogo", "go")
                .On(true).FallthroughVariation(1)
                .Prerequisites(new Prerequisite(cycleTargetKey, 1)) // deliberate error
                .Build();

            var logCapture = Logs.Capture();
            var evaluator = BasicEvaluator.WithStoredFlags(f1, f2).WithLogger(logCapture.Logger(""));
            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);

            AssertHelpers.LogMessageRegex(logCapture, true, LogLevel.Error,
                ".*prerequisite relationship.*circular reference.*");
        }
    }
}
