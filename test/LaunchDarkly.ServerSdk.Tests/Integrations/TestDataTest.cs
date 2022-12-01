using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.Model.TargetBuilder;
using static LaunchDarkly.TestHelpers.JsonAssertions;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class TestDataTest : BaseTest
    {
        private static readonly LdValue[] ThreeStringValues = new LdValue[]
        {
            LdValue.Of("red"), LdValue.Of("green"), LdValue.Of("blue")
        };

        private readonly TestData _td = TestData.DataSource();
        private readonly CapturingDataSourceUpdates _updates = new CapturingDataSourceUpdates();

        public TestDataTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void InitializesWithEmptyData()
        {
            CreateAndStart();

            var data = TestUtils.NormalizeDataSet(_updates.Inits.ExpectValue());
            Assert.Collection(data.Data,
                coll =>
                {
                    Assert.Equal(DataModel.Features, coll.Key);
                    Assert.Empty(coll.Value.Items);
                },
                coll =>
                {
                    Assert.Equal(DataModel.Segments, coll.Key);
                    Assert.Empty(coll.Value.Items);
                });
        }

        [Fact]
        public void InitializesWithFlags()
        {
            _td.Update(_td.Flag("flag1").On(true))
                .Update(_td.Flag("flag2").On(false));

            CreateAndStart();

            var data = TestUtils.NormalizeDataSet(_updates.Inits.ExpectValue());
            Assert.Collection(data.Data,
                coll =>
                {
                    Assert.Equal(DataModel.Features, coll.Key);
                    Assert.Collection(coll.Value.Items,
                        FlagItemAssertion("flag1", 1, json =>
                            Assert.Equal(LdValue.Of(true), json.Get("on"))),
                        FlagItemAssertion("flag2", 1, json =>
                            Assert.Equal(LdValue.Of(false), json.Get("on")))
                        );
                },
                coll =>
                {
                    Assert.Equal(DataModel.Segments, coll.Key);
                    Assert.Empty(coll.Value.Items);
                });
        }

        [Fact]
        public void AddsFlag()
        {
            CreateAndStart();

            _td.Update(_td.Flag("flag1").On(true));

            var up = _updates.Upserts.ExpectValue();
            Assert.Equal(DataModel.Features, up.Kind);
            AssertFlag("flag1", 1, up.Key, up.Item, json =>
                Assert.Equal(LdValue.Of(true), json.Get("on")));
        }

        [Fact]
        public void UpdatesFlag()
        {
            _td.Update(_td.Flag("flag1").On(true));

            CreateAndStart();
            _updates.Upserts.ExpectNoValue();

            _td.Update(_td.Flag("flag1").On(true));

            var up = _updates.Upserts.ExpectValue();
            Assert.Equal(DataModel.Features, up.Kind);
            AssertFlag("flag1", 2, up.Key, up.Item, json =>
                Assert.Equal(LdValue.Of(true), json.Get("on")));
        }

        [Fact]
        public void FlagConfigSimpleBoolean()
        {
            Func<FeatureFlagBuilder, FeatureFlagBuilder> expectedBooleanFlag = fb =>
                fb.On(true).Variations(true, false).OffVariation(1).FallthroughVariation(0);

            VerifyFlag(f => f, expectedBooleanFlag);
            VerifyFlag(f => f.BooleanFlag(), expectedBooleanFlag);
            VerifyFlag(f => f.On(true), expectedBooleanFlag);
            VerifyFlag(f => f.On(false), fb => expectedBooleanFlag(fb).On(false));
            VerifyFlag(f => f.VariationForAll(false), fb => expectedBooleanFlag(fb).FallthroughVariation(1));
            VerifyFlag(f => f.VariationForAll(true), expectedBooleanFlag);

            VerifyFlag(f => f.FallthroughVariation(true).OffVariation(false), expectedBooleanFlag);

            VerifyFlag(
                f => f.FallthroughVariation(false).OffVariation(true),
                fb => expectedBooleanFlag(fb).FallthroughVariation(1).OffVariation(0)
                );
        }

        [Fact]
        public void UsingBooleanConfigMethodsForcesFlagToBeBoolean()
        {
            Func<FeatureFlagBuilder, FeatureFlagBuilder> expectedBooleanFlag = fb =>
                fb.On(true).Variations(true, false).OffVariation(1).FallthroughVariation(0);

            VerifyFlag(
                f => f.Variations(LdValue.Of(1), LdValue.Of(2))
                    .BooleanFlag(),
                expectedBooleanFlag
                );
            VerifyFlag(
                f => f.Variations(LdValue.Of(true), LdValue.Of(2))
                    .BooleanFlag(),
                expectedBooleanFlag
                );
            VerifyFlag(
                f => f.BooleanFlag(),
                expectedBooleanFlag
                );
        }

        [Fact]
        public void FlagConfigStringVariations()
        {
            VerifyFlag(
                f => f.Variations(ThreeStringValues).OffVariation(0).FallthroughVariation(2),
                fb => fb.Variations("red", "green", "blue").On(true).OffVariation(0).FallthroughVariation(2)
                );
        }

        [Fact]
        public void UserTargets()
        {
            Func<FeatureFlagBuilder, FeatureFlagBuilder> expectedBooleanFlag = fb =>
                fb.Variations(true, false).On(true).OffVariation(1).FallthroughVariation(0);

            VerifyFlag(
                f => f.VariationForUser("a", true).VariationForUser("b", true),
                fb => expectedBooleanFlag(fb).Targets(UserTarget(0, "a", "b")).
                    ContextTargets(ContextTarget(ContextKind.Default, 0))
                );
            VerifyFlag(
                f => f.VariationForUser("a", true).VariationForUser("a", true),
                fb => expectedBooleanFlag(fb).Targets(UserTarget(0, "a")).
                    ContextTargets(ContextTarget(ContextKind.Default, 0))
                );
            VerifyFlag(
                f => f.VariationForUser("a", true).VariationForUser("a", false),
                fb => expectedBooleanFlag(fb).Targets(UserTarget(1, "a")).
                    ContextTargets(ContextTarget(ContextKind.Default, 1))
                );
            VerifyFlag(
                f => f.VariationForUser("a", false).VariationForUser("b", true).VariationForUser("c", false),
                fb => expectedBooleanFlag(fb).Targets(UserTarget(0, "b"), UserTarget(1, "a", "c")).
                    ContextTargets(ContextTarget(ContextKind.Default, 0), ContextTarget(ContextKind.Default, 1))
                );
            VerifyFlag(
                f => f.VariationForUser("a", true).VariationForUser("b", true).VariationForUser("a", false),
                fb => expectedBooleanFlag(fb).Targets(UserTarget(0, "b"), UserTarget(1, "a")).
                    ContextTargets(ContextTarget(ContextKind.Default, 0), ContextTarget(ContextKind.Default, 1))
                );

            Func<FeatureFlagBuilder, FeatureFlagBuilder> expectedStringFlag = fb =>
                fb.Variations("red", "green", "blue").On(true).OffVariation(0).FallthroughVariation(2);

            VerifyFlag(
                f => f.Variations(ThreeStringValues).OffVariation(0).FallthroughVariation(2)
                    .VariationForUser("a", 2).VariationForUser("b", 2),
                fb => expectedStringFlag(fb).Targets(UserTarget(2, "a", "b")).
                    ContextTargets(ContextTarget(ContextKind.Default, 2))
                );
            VerifyFlag(
                f => f.Variations(ThreeStringValues).OffVariation(0).FallthroughVariation(2)
                    .VariationForUser("a", 2).VariationForUser("b", 1).VariationForUser("c", 2),
                fb => expectedStringFlag(fb).Targets(UserTarget(1, "b"), UserTarget(2, "a", "c")).
                    ContextTargets(ContextTarget(ContextKind.Default, 1), ContextTarget(ContextKind.Default, 2))
                );
        }

        [Fact]
        public void ContextTargets()
        {
            ContextKind kind1 = ContextKind.Of("org"), kind2 = ContextKind.Of("other");

            Func<FeatureFlagBuilder, FeatureFlagBuilder> expectedBooleanFlag = fb =>
                fb.Variations(true, false).On(true).OffVariation(1).FallthroughVariation(0);

            VerifyFlag(
                f => f.VariationForKey(kind1, "a", true).VariationForKey(kind1, "b", true),
                fb => expectedBooleanFlag(fb).ContextTargets(ContextTarget(kind1, 0, "a", "b"))
                );
            VerifyFlag(
                f => f.VariationForKey(kind1, "a", true).VariationForKey(kind2, "a", true),
                fb => expectedBooleanFlag(fb).ContextTargets(ContextTarget(kind1, 0, "a"), ContextTarget(kind2, 0, "a"))
                );
            VerifyFlag(
                f => f.VariationForKey(kind1, "a", true).VariationForKey(kind1, "a", true),
                fb => expectedBooleanFlag(fb).ContextTargets(ContextTarget(kind1, 0, "a"))
                );
            VerifyFlag(
                f => f.VariationForKey(kind1, "a", true).VariationForKey(kind1, "a", false),
                fb => expectedBooleanFlag(fb).ContextTargets(ContextTarget(kind1, 1, "a"))
                );

            Func<FeatureFlagBuilder, FeatureFlagBuilder> expectedStringFlag = fb =>
                fb.Variations("red", "green", "blue").On(true).OffVariation(0).FallthroughVariation(2);

            VerifyFlag(
                f => f.Variations(ThreeStringValues).OffVariation(0).FallthroughVariation(2)
                    .VariationForKey(kind1, "a", 2).VariationForKey(kind1, "b", 2),
                fb => expectedStringFlag(fb).ContextTargets(ContextTarget(kind1, 2, "a", "b"))
                );
        }

        [Fact]
        public void FlagRules()
        {
            Func<FeatureFlagBuilder, FeatureFlagBuilder> expectedBooleanFlag = fb =>
                fb.Variations(true, false).On(true).OffVariation(1).FallthroughVariation(0);

            // match that returns variation 0/true
            Func<FeatureFlagBuilder, FeatureFlagBuilder> matchReturnsVariation0 = fb =>
                expectedBooleanFlag(fb).Rules(new RuleBuilder().Id("rule0").Variation(0).Clauses(
                    new ClauseBuilder().Attribute("name").Op("in").Values("Lucy").Build()
                    ).Build());
            VerifyFlag(
                f => f.IfMatch("name", LdValue.Of("Lucy")).ThenReturn(true),
                matchReturnsVariation0
                );
            VerifyFlag(
                f => f.IfMatch("name", LdValue.Of("Lucy")).ThenReturn(0),
                matchReturnsVariation0
                );

            // match that returns variation 1/false
            Func<FeatureFlagBuilder, FeatureFlagBuilder> matchReturnsVariation1 = fb =>
                expectedBooleanFlag(fb).Rules(new RuleBuilder().Id("rule0").Variation(1).Clauses(
                    new ClauseBuilder().Attribute("name").Op("in").Values("Lucy").Build()
                    ).Build());
            VerifyFlag(
                f => f.IfMatch("name", LdValue.Of("Lucy")).ThenReturn(false),
                matchReturnsVariation1
                );
            VerifyFlag(
                f => f.IfMatch("name", LdValue.Of("Lucy")).ThenReturn(1),
                matchReturnsVariation1
                );

            // negated match
            VerifyFlag(
                f => f.IfNotMatch("name", LdValue.Of("Lucy")).ThenReturn(true),
                fb => expectedBooleanFlag(fb).Rules(new RuleBuilder().Id("rule0").Variation(0).Clauses(
                    new ClauseBuilder().Attribute("name").Op("in").Values("Lucy").Negate(true).Build()
                    ).Build())
                );

            // multiple clauses
            VerifyFlag(
                f => f.IfMatch("name", LdValue.Of("Lucy"))
                    .AndMatch("country", LdValue.Of("gb"))
                    .ThenReturn(true),
                fb => expectedBooleanFlag(fb).Rules(new RuleBuilder().Id("rule0").Variation(0).Clauses(
                    new ClauseBuilder().Attribute("name").Op("in").Values("Lucy").Build(),
                    new ClauseBuilder().Attribute("country").Op("in").Values("gb").Build()
                    ).Build())
                );

            // multiple rules
            VerifyFlag(
                f => f.IfMatch("name", LdValue.Of("Lucy")).ThenReturn(true)
                    .IfMatch("name", LdValue.Of("Mina")).ThenReturn(false),
                fb => expectedBooleanFlag(fb).Rules(
                    new RuleBuilder().Id("rule0").Variation(0).Clauses(
                        new ClauseBuilder().Attribute("name").Op("in").Values("Lucy").Build()
                        ).Build(),
                    new RuleBuilder().Id("rule1").Variation(1).Clauses(
                        new ClauseBuilder().Attribute("name").Op("in").Values("Mina").Build()
                        ).Build())
                );
        }

        private void CreateAndStart()
        {
            var ds = _td.Build(BasicContext.WithDataSourceUpdates(_updates));
            var started = ds.Start();
            Assert.True(started.IsCompleted);
            Assert.Equal(DataSourceState.Valid, _updates.StatusUpdates.ExpectValue().State);
        }

        private void AssertFlag(string expectedKey, int version, string actualKey, ItemDescriptor item, Action<LdValue> jsonAssertions)
        {
            Assert.Equal(expectedKey, actualKey);
            Assert.Equal(version, item.Version);
            var json = LdValue.Parse(DataModel.Features.Serialize(item));
            Assert.Equal(expectedKey, json.Get("key").AsString);
            jsonAssertions(json);
        }

        private Action<KeyValuePair<string, ItemDescriptor>> FlagItemAssertion(string expectedKey, int version, Action<LdValue> jsonAssertions) =>
            kv => AssertFlag(expectedKey, version, kv.Key, kv.Value, jsonAssertions);

        private void VerifyFlag(Func<TestData.FlagBuilder, TestData.FlagBuilder> configureFlag,
            Func<FeatureFlagBuilder, FeatureFlagBuilder> configureExpectedFlag)
        {
            var expectedFlag = new FeatureFlagBuilder("flagkey").Version(1).Salt("");
            expectedFlag = configureExpectedFlag(expectedFlag);
            var expectedJson = DataModel.Features.Serialize(TestUtils.DescriptorOf(expectedFlag.Build()));

            var td = TestData.DataSource();
            td.Build(BasicContext.WithDataSourceUpdates(_updates)).Start();

            td.Update(configureFlag(_td.Flag("flagkey")));

            var up = _updates.Upserts.ExpectValue();
            AssertJsonEqual(expectedJson, DataModel.Features.Serialize(up.Item));
        }
    }
}
