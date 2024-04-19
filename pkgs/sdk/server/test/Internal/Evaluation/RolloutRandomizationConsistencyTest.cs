﻿using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    public class RolloutRandomizationConsistencyTest
    {
        // Note: These tests are meant to be exact duplicates of tests in other SDKs.
        // Do not change any of the values unless they are also changed in other SDKs.
        // These are not traditional behavioral tests so much as consistency tests to
        // guarantee that the implementation is identical across SDKs.

        private const int decimalPlacesOfEquality = 6;
        private static readonly int? seed = 61; // seed chosen to ensure users fall into different buckets
        private static readonly int? noSeed = null;

        private static Rollout BuildRollout(RolloutKind kind, bool untrackedVariations)
        {
            var variations = new List<WeightedVariation>()
            {
                new WeightedVariation(0, 10000, untrackedVariations),
                new WeightedVariation(1, 20000, untrackedVariations),
                new WeightedVariation(0, 70000, true)
            };
            return new Rollout(kind, null, seed, variations, new AttributeRef());
        }

        [Fact]
        public void VariationIndexForUserInExperimentTest()
        {
            var rollout = BuildRollout(RolloutKind.Experiment, false);
            const string key = "hashKey";
            const string salt = "saltyA";

            var user1 = Context.New("userKeyA");
            // bucketVal = 0.09801207
            AssertVariationIndexAndExperimentStateForRollout(0, true, rollout, user1, key, salt);

            var user2 = Context.New("userKeyB");
            // bucketVal = 0.14483777
            AssertVariationIndexAndExperimentStateForRollout(1, true, rollout, user2, key, salt);

            var user3 = Context.New("userKeyC");
            // bucketVal = 0.9242641
            AssertVariationIndexAndExperimentStateForRollout(0, false, rollout, user3, key, salt);
        }

        private static void AssertVariationIndexAndExperimentStateForRollout(
            int expectedVariation,
            bool expectedInExperiment,
            Rollout rollout,
            Context context,
            string flagKey,
            string salt
            )
        {
            var flag = new FeatureFlagBuilder(flagKey)
                .On(true)
                .GeneratedVariations(3)
                .FallthroughRollout(rollout)
                .Salt(salt)
                .Build();
            var result = BasicEvaluator.Evaluate(flag, context);
            Assert.Equal(expectedVariation, result.Result.VariationIndex);
            Assert.Equal(EvaluationReasonKind.Fallthrough, result.Result.Reason.Kind);
            Assert.Equal(expectedInExperiment, result.Result.Reason.InExperiment);
        }

        [Fact]
        public void BucketUserByKeyTest()
        {
            var user1 = Context.New("userKeyA");
            var point1 = Bucketing.ComputeBucketValue(false, noSeed, user1, null, "hashKey", null, "saltyA") ?? 0;
            Assert.Equal(0.42157587, point1, decimalPlacesOfEquality);

            var user2 = Context.New("userKeyB");
            var point2 = Bucketing.ComputeBucketValue(false, noSeed, user2, null, "hashKey", null, "saltyA") ?? 0;
            Assert.Equal(0.6708485, point2, decimalPlacesOfEquality);

            var user3 = Context.New("userKeyC");
            var point3 = Bucketing.ComputeBucketValue(false, noSeed, user3, null, "hashKey", null, "saltyA") ?? 0;
            Assert.Equal(0.10343106, point3, decimalPlacesOfEquality);
        }

        [Fact]
        public void BucketUserWithSeedTest()
        {
            const int seed = 61;

            var user1 = Context.New("userKeyA");
            var point1 = Bucketing.ComputeBucketValue(false, seed, user1, null, "hashKey", null, "saltyA") ?? 0;
            Assert.Equal(0.09801207, point1, decimalPlacesOfEquality);

            var user2 = Context.New("userKeyB");
            var point2 = Bucketing.ComputeBucketValue(false, seed, user2, null, "hashKey", null, "saltyA") ?? 0;
            Assert.Equal(0.14483777, point2, decimalPlacesOfEquality);

            var user3 = Context.New("userKeyC");
            var point3 = Bucketing.ComputeBucketValue(false, seed, user3, null, "hashKey", null, "saltyA") ?? 0;
            Assert.Equal(0.9242641, point3, decimalPlacesOfEquality);
        }
    }
}
