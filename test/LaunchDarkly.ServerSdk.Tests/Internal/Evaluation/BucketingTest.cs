using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    public class BucketingTest
    {
        [Fact]
        public void VariationIndexForBucket()
        {
            var user = Context.New("userkey");
            const string flagKey = "flagkey";
            const string salt = "salt";

            // First verify that with our test inputs, the bucket value will be greater than zero and less than 100000,
            // so we can construct a rollout whose second bucket just barely contains that value
            var bucketValue = (int)(Bucketing.ComputeBucketValue(false, null, user, null, flagKey, null, salt) * 100000);
            Assert.InRange(bucketValue, 1, 99999);

            const int badVariationA = 0, matchedVariation = 1, badVariationB = 2;
            var variations = new List<WeightedVariation>()
            {
                new WeightedVariation(badVariationA, bucketValue, true), // end of bucket range is not inclusive, so it will *not* match the target value
                new WeightedVariation(matchedVariation, 1, true), // size of this bucket is 1, so it only matches that specific value
                new WeightedVariation(badVariationB, 100000 - (bucketValue + 1), true)
            };
            var rollout = new Rollout(RolloutKind.Rollout, null, null, variations, new AttributeRef());
            AssertVariationIndexFromRollout(matchedVariation, rollout, user, flagKey, salt);
        }

        [Fact]
        public void UsingSeedIsDifferentThanSalt()
        {
            var user = Context.New("userkey");
            const string flagKey = "flagkey";
            const string salt = "salt";
            const int seed = 123;

            var bucketValue1 = Bucketing.ComputeBucketValue(false, null, user, null, flagKey, null, salt);
            var bucketValue2 = Bucketing.ComputeBucketValue(false, seed, user, null, flagKey, null, salt);
            Assert.NotEqual(bucketValue1, bucketValue2);
        }

        [Fact]
        public void DifferentSeedsProduceDifferentAssignment()
        {
            var user = Context.New("userkey");
            const string flagKey = "flagkey";
            const string salt = "salt";
            const int seed1 = 123, seed2 = 456;

            var bucketValue1 = Bucketing.ComputeBucketValue(false, seed1, user, null, flagKey, null, salt);
            var bucketValue2 = Bucketing.ComputeBucketValue(false, seed2, user, null, flagKey, null, salt);
            Assert.NotEqual(bucketValue1, bucketValue2);
        }

        [Fact]
        public void FlagKeyAndSaltDoNotMatterWhenSeedIsUsed()
        {
            var user = Context.New("userkey");
            const string flagKey1 = "flagkey", flagKey2 = "flagkey2";
            const string salt1 = "salt", salt2 = "salt2";
            const int seed = 123;

            var bucketValue1 = Bucketing.ComputeBucketValue(false, seed, user, null, flagKey1, null, salt1);
            var bucketValue2 = Bucketing.ComputeBucketValue(false, seed, user, null, flagKey2, null, salt2);
            Assert.Equal(bucketValue1, bucketValue2);
        }

        [Fact]
        public void LastBucketIsUsedIfBucketValueEqualsTotalWeight()
        {
            var user = Context.New("userkey");
            const string flagKey = "flagkey";
            const string salt = "salt";

            // We'll construct a list of variations that stops right at the target bucket value
            int bucketValue = (int)(Bucketing.ComputeBucketValue(false, null, user, null, flagKey, null, salt) * 100000);

            var variations = new List<WeightedVariation>()
            {
                new WeightedVariation(0, bucketValue, true)
            };
            var rollout = new Rollout(RolloutKind.Rollout, null, null, variations, new AttributeRef());

            AssertVariationIndexFromRollout(0, rollout, user, flagKey, salt);
        }

        [Fact]
        public void CanBucketByIntAttributeSameAsString()
        {
            var user = Context.Builder("key")
                .Set("stringattr", "33333")
                .Set("intattr", 33333)
                .Build();
            var resultForString = Bucketing.ComputeBucketValue(false, null, user, null, "key", AttributeRef.FromLiteral("stringattr"), "salt");
            var resultForInt = Bucketing.ComputeBucketValue(false, null, user, null, "key", AttributeRef.FromLiteral("intattr"), "salt");
            Assert.Equal((double)resultForInt, (double)resultForString, 10);

            var multiContext = Context.NewMulti(
                Context.New(kind1, "key1"),
                Context.Builder("key2").Kind("kind2")
                    .Set("stringattr", "33333")
                    .Set("intattr", 33333)
                    .Build());
            var resultForString1 = Bucketing.ComputeBucketValue(false, null, multiContext, kind2, "key", AttributeRef.FromLiteral("stringattr"), "salt");
            var resultForInt1 = Bucketing.ComputeBucketValue(false, null, multiContext, kind2, "key", AttributeRef.FromLiteral("intattr"), "salt");
            Assert.Equal(resultForString, resultForString1);
            Assert.Equal(resultForInt, resultForInt1);
        }

        [Fact]
        public void CannotBucketByOtherDataTypes()
        {
            foreach (var attributeValue in new LdValue[] {
                LdValue.Null,
                LdValue.Of(true),
                LdValue.Of(33333.5)
            })
            {
                var user = Context.Builder("key")
                    .Set("badattr", attributeValue)
                    .Build();
                var result = Bucketing.ComputeBucketValue(false, null, user, null, "key", AttributeRef.FromLiteral("badattr"), "salt");
                if (result != 0f)
                {
                    Assert.True(false, "got unexpected value " + result + " for attribute value " + attributeValue);
                }
            }
        }

        [Fact]
        public void SecondaryKeyAffectsBucketValueForRollout()
        {
            var user1 = Context.New("key");
            var user2 = Context.Builder("key").Secondary("other").Build();
            const string flagKey = "flagkey";
            const string salt = "salt";

            var result1 = Bucketing.ComputeBucketValue(false, null, user1, null, flagKey, null, salt);
            var result2 = Bucketing.ComputeBucketValue(false, null, user2, null, flagKey, null, salt);
            Assert.NotEqual(result1, result2);
        }

        [Fact]
        public void SecondaryKeyDoesNotAffectBucketValueForExperiment()
        {
            var user1 = Context.New("key");
            var user2 = Context.Builder("key").Secondary("other").Build();
            const string flagKey = "flagkey";
            const string salt = "salt";

            var result1 = Bucketing.ComputeBucketValue(true, null, user1, null, flagKey, null, salt);
            var result2 = Bucketing.ComputeBucketValue(true, null, user2, null, flagKey, null, salt);
            Assert.Equal(result1, result2);
        }

        private static void AssertVariationIndexFromRollout(
            int expectedVariation,
            Rollout rollout,
            Context context,
            string flagKey,
            string salt
            )
        {
            var flag1 = new FeatureFlagBuilder(flagKey)
                .On(true)
                .GeneratedVariations(3)
                .FallthroughRollout(rollout)
                .Salt(salt)
                .Build();
            var result1 = BasicEvaluator.Evaluate(flag1, context);
            Assert.Equal(EvaluationReason.FallthroughReason, result1.Result.Reason);
            Assert.Equal(expectedVariation, result1.Result.VariationIndex);

            // Make sure we consistently apply the rollout regardless of whether it's in a rule or a fallthrough
            var flag2 = new FeatureFlagBuilder(flagKey)
                .On(true)
                .GeneratedVariations(3)
                .Rules(new RuleBuilder().Id("id")
                    .Rollout(rollout)
                    .Clauses(new ClauseBuilder().Attribute("key").Op(Operator.In).Values(context.Key).Build())
                    .Build())
                .Salt(salt)
                .Build();
            var result2 = BasicEvaluator.Evaluate(flag2, context);
            Assert.Equal(EvaluationReason.RuleMatchReason(0, "id"), result2.Result.Reason);
            Assert.Equal(expectedVariation, result2.Result.VariationIndex);
        }
    }
}
