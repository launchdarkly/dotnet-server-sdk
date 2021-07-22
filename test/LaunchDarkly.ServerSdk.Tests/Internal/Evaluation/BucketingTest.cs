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
            var user = User.WithKey("userkey");
            const string flagKey = "flagkey";
            const string salt = "salt";

            // First verify that with our test inputs, the bucket value will be greater than zero and less than 100000,
            // so we can construct a rollout whose second bucket just barely contains that value
            var bucketValue = (int)(Bucketing.BucketUser(null, user, flagKey, UserAttribute.Key, salt) * 100000);
            Assert.InRange(bucketValue, 1, 99999);

            const int badVariationA = 0, matchedVariation = 1, badVariationB = 2;
            var variations = new List<WeightedVariation>()
            {
                new WeightedVariation(badVariationA, bucketValue, true), // end of bucket range is not inclusive, so it will *not* match the target value
                new WeightedVariation(matchedVariation, 1, true), // size of this bucket is 1, so it only matches that specific value
                new WeightedVariation(badVariationB, 100000 - (bucketValue + 1), true)
            };
            var rollout = new Rollout(RolloutKind.Rollout, null, variations, null);
            AssertVariationIndexFromRollout(matchedVariation, rollout, user, flagKey, salt);
        }

        [Fact]
        public void UsingSeedIsDifferentThanSalt()
        {
            var user = User.WithKey("userkey");
            const string flagKey = "flagkey";
            const string salt = "salt";
            const int seed = 123;

            var bucketValue1 = Bucketing.BucketUser(null, user, flagKey, UserAttribute.Key, salt);
            var bucketValue2 = Bucketing.BucketUser(seed, user, flagKey, UserAttribute.Key, salt);
            Assert.NotEqual(bucketValue1, bucketValue2);
        }

        [Fact]
        public void DifferentSeedsProduceDifferentAssignment()
        {
            var user = User.WithKey("userkey");
            const string flagKey = "flagkey";
            const string salt = "salt";
            const int seed1 = 123, seed2 = 456;

            var bucketValue1 = Bucketing.BucketUser(seed1, user, flagKey, UserAttribute.Key, salt);
            var bucketValue2 = Bucketing.BucketUser(seed2, user, flagKey, UserAttribute.Key, salt);
            Assert.NotEqual(bucketValue1, bucketValue2);
        }

        [Fact]
        public void FlagKeyAndSaltDoNotMatterWhenSeedIsUsed()
        {
            var user = User.WithKey("userkey");
            const string flagKey1 = "flagkey", flagKey2 = "flagkey2";
            const string salt1 = "salt", salt2 = "salt2";
            const int seed = 123;

            var bucketValue1 = Bucketing.BucketUser(seed, user, flagKey1, UserAttribute.Key, salt1);
            var bucketValue2 = Bucketing.BucketUser(seed, user, flagKey2, UserAttribute.Key, salt2);
            Assert.Equal(bucketValue1, bucketValue2);
        }

        [Fact]
        public void LastBucketIsUsedIfBucketValueEqualsTotalWeight()
        {
            var user = User.WithKey("userkey");
            const string flagKey = "flagkey";
            const string salt = "salt";

            // We'll construct a list of variations that stops right at the target bucket value
            int bucketValue = (int)(Bucketing.BucketUser(null, user, flagKey, UserAttribute.Key, salt) * 100000);

            var variations = new List<WeightedVariation>()
            {
                new WeightedVariation(0, bucketValue, true)
            };
            var rollout = new Rollout(RolloutKind.Rollout, null, variations, null);

            AssertVariationIndexFromRollout(0, rollout, user, flagKey, salt);
        }

        [Fact]
        public void CanBucketByIntAttributeSameAsString()
        {
            var user = User.Builder("key")
                .Custom("stringattr", "33333")
                .Custom("intattr", 33333)
                .Build();
            var resultForString = Bucketing.BucketUser(null, user, "key", UserAttribute.ForName("stringattr"), "salt");
            var resultForInt = Bucketing.BucketUser(null, user, "key", UserAttribute.ForName("intattr"), "salt");
            Assert.Equal((double)resultForInt, (double)resultForString, 10);
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
                var user = User.Builder("key")
                    .Custom("badattr", attributeValue)
                    .Build();
                var result = Bucketing.BucketUser(null, user, "key", UserAttribute.ForName("badattr"), "salt");
                if (result != 0f)
                {
                    Assert.True(false, "got unexpected value " + result + " for attribute value " + attributeValue);
                }
            }
        }

        [Fact]
        public void UserSecondaryKeyAffectsBucketValue()
        {
            var user1 = User.WithKey("key");
            var user2 = User.Builder("key").Secondary("other").Build();
            const string flagKey = "flagkey";
            const string salt = "salt";

            var result1 = Bucketing.BucketUser(null, user1, flagKey, UserAttribute.Key, salt);
            var result2 = Bucketing.BucketUser(null, user2, flagKey, UserAttribute.Key, salt);
            Assert.NotEqual(result1, result2);
        }

        private static void AssertVariationIndexFromRollout(
            int expectedVariation,
            Rollout rollout,
            User user,
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
            var result1 = BasicEvaluator.Evaluate(flag1, user, EventFactory.Default);
            Assert.Equal(EvaluationReason.FallthroughReason, result1.Result.Reason);
            Assert.Equal(expectedVariation, result1.Result.VariationIndex);

            // Make sure we consistently apply the rollout regardless of whether it's in a rule or a fallthrough
            var flag2 = new FeatureFlagBuilder(flagKey)
                .On(true)
                .GeneratedVariations(3)
                .Rules(new RuleBuilder().Id("id")
                    .Rollout(rollout)
                    .Clauses(new ClauseBuilder().Attribute(UserAttribute.Key).Op(Operator.In).Values(LdValue.Of(user.Key)).Build())
                    .Build())
                .Salt(salt)
                .Build();
            var result2 = BasicEvaluator.Evaluate(flag2, user, EventFactory.Default);
            Assert.Equal(EvaluationReason.RuleMatchReason(0, "id"), result2.Result.Reason);
            Assert.Equal(expectedVariation, result2.Result.VariationIndex);
        }
    }
}
