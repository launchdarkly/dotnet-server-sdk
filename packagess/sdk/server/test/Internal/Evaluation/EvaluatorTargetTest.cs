using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    // Tests of flag evaluation involving context targets.

    public class EvaluatorTargetTest
    {
        private const int FallthroughVar = 0, MatchVar1 = 1, MatchVar2 = 2;
        private static readonly LdValue[] Variations = new LdValue[] {
            LdValue.Of("fallthrough"), LdValue.Of("match1"), LdValue.Of("match2") };
        private static readonly Context baseUser = Context.New("userkey");
        private static readonly LdValue fallthroughValue = LdValue.Of("fallthrough");
        private static readonly LdValue offValue = LdValue.Of("off");
        private static readonly LdValue Value = LdValue.Of("on");
        private static readonly ContextKind DogKind = ContextKind.Of("dog"), CatKind = ContextKind.Of("cat");

        [Fact]
        public void UserTargetsOnly()
        {
            var f = BaseFlagBuilder()
                .Targets(
                    TargetBuilder.UserTarget(MatchVar1, "c"),
                    TargetBuilder.UserTarget(MatchVar2, "b", "a")
                )
                .Build();

            ExpectMatch(f, User("a"), MatchVar2);
            ExpectMatch(f, User("b"), MatchVar2);
            ExpectMatch(f, User("c"), MatchVar1);
            ExpectFallthrough(f, User("z"));

            ExpectMatch(f, Context.NewMulti(Dog("b"), User("a")), MatchVar2);
            ExpectMatch(f, Context.NewMulti(Dog("a"), User("c")), MatchVar1);
            ExpectFallthrough(f, Context.NewMulti(Dog("b"), User("z")));
            ExpectFallthrough(f, Context.NewMulti(Dog("a"), Cat("b")));
        }

        [Fact]
        public void UserTargetsAndContextTargets()
        {
            var f = BaseFlagBuilder()
                .Targets(
                    TargetBuilder.UserTarget(MatchVar1, "c"),
                    TargetBuilder.UserTarget(MatchVar2, "b", "a")
                    )
                .ContextTargets(
                    TargetBuilder.ContextTarget(DogKind, MatchVar1, "a", "b"),
                    TargetBuilder.ContextTarget(DogKind, MatchVar2, "c"),
                    TargetBuilder.ContextTarget(ContextKind.Default, MatchVar1),
                    TargetBuilder.ContextTarget(ContextKind.Default, MatchVar2)
                    )
                .Build();

            ExpectMatch(f, User("a"), MatchVar2);
            ExpectMatch(f, User("b"), MatchVar2);
            ExpectMatch(f, User("c"), MatchVar1);
            ExpectFallthrough(f, User("z"));

            ExpectMatch(f, Context.NewMulti(Dog("b"), User("a")), MatchVar1); // the "dog" target takes precedence due to ordering
            ExpectMatch(f, Context.NewMulti(Dog("z"), User("a")), MatchVar2); // "dog" targets don't match, continue to "user" targets
            ExpectFallthrough(f, Context.NewMulti(Dog("x"), User("z"))); // nothing matches
            ExpectMatch(f, Context.NewMulti(Dog("a"), Cat("b")), MatchVar1);
        }

        private static FeatureFlagBuilder BaseFlagBuilder() =>
            new FeatureFlagBuilder("feature").On(true).Variations(Variations).FallthroughVariation(0).OffVariation(0);

        private static Context User(string key) => Context.New(key);

        private static Context Dog(string key) => Context.New(DogKind, key);

        private static Context Cat(string key) => Context.New(CatKind, key);

        private static void ExpectMatch(FeatureFlag f, Context c, int variation)
        {
            var result = BasicEvaluator.Evaluate(f, c);
            Assert.Equal(variation, result.Result.VariationIndex);
            Assert.Equal(Variations[variation], result.Result.Value);
            Assert.Equal(EvaluationReason.TargetMatchReason, result.Result.Reason);
        }

        private static void ExpectFallthrough(FeatureFlag f, Context c)
        {
            var result = BasicEvaluator.Evaluate(f, c);
            Assert.Equal(FallthroughVar, result.Result.VariationIndex);
            Assert.Equal(Variations[FallthroughVar], result.Result.Value);
            Assert.Equal(EvaluationReason.FallthroughReason, result.Result.Reason);
        }
    }
}
