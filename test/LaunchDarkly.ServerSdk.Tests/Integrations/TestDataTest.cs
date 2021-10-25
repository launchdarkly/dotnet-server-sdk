using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
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
            string basicProps = "\"variations\":[true,false],\"offVariation\":1,\"rules\":[],\"targets\":[]",
                onProps = basicProps + ",\"on\":true",
                offProps = basicProps + ",\"on\":false",
                fallthroughTrue = ",\"fallthrough\":{\"variation\":0}",
                fallthroughFalse = ",\"fallthrough\":{\"variation\":1}";

            VerifyFlag(f => f, onProps + fallthroughTrue);
            VerifyFlag(f => f.BooleanFlag(), onProps + fallthroughTrue);
            VerifyFlag(f => f.On(true), onProps + fallthroughTrue);
            VerifyFlag(f => f.On(false), offProps + fallthroughTrue);
            VerifyFlag(f => f.VariationForAllUsers(false), onProps + fallthroughFalse);
            VerifyFlag(f => f.VariationForAllUsers(true), onProps + fallthroughTrue);

            VerifyFlag(
                f => f.FallthroughVariation(true).OffVariation(false),
                onProps + fallthroughTrue
                );

            VerifyFlag(
                f => f.FallthroughVariation(false).OffVariation(true),
                "\"variations\":[true,false],\"on\":true,\"offVariation\":0,\"fallthrough\":{\"variation\":1}"
                    + ",\"rules\":[],\"targets\":[]"
                );
        }

        [Fact]
        public void UsingBooleanConfigMethodsForcesFlagToBeBoolean()
        {
            string booleanProps = "\"on\":true,\"rules\":[],\"targets\":[]"
                + ",\"variations\":[true,false],\"offVariation\":1,\"fallthrough\":{\"variation\":0}";

            VerifyFlag(
                f => f.Variations(LdValue.Of(1), LdValue.Of(2))
                    .BooleanFlag(),
                booleanProps
                );
            VerifyFlag(
                f => f.Variations(LdValue.Of(true), LdValue.Of(2))
                    .BooleanFlag(),
                booleanProps
                );
            VerifyFlag(
                f => f.BooleanFlag(),
                booleanProps
                );
        }

        [Fact]
        public void FlagConfigStringVariations()
        {
            string basicProps = "\"variations\":[\"red\",\"green\",\"blue\"],\"on\":true"
                + ",\"offVariation\":0,\"fallthrough\":{\"variation\":2},\"rules\":[],\"targets\":[]";

            VerifyFlag(
                f => f.Variations(ThreeStringValues).OffVariation(0).FallthroughVariation(2),
                basicProps
                );
        }

        [Fact]
        public void UserTargets()
        {
            string booleanFlagBasicProps = "\"on\":true,\"variations\":[true,false],\"rules\":[]" +
                ",\"offVariation\":1,\"fallthrough\":{\"variation\":0}";
            VerifyFlag(
                f => f.VariationForUser("a", true).VariationForUser("b", true),
                booleanFlagBasicProps + ",\"targets\":[{\"variation\":0,\"values\":[\"a\",\"b\"]}]"
                );
            VerifyFlag(
                f => f.VariationForUser("a", true).VariationForUser("a", true),
                booleanFlagBasicProps + ",\"targets\":[{\"variation\":0,\"values\":[\"a\"]}]"
                );
            VerifyFlag(
                f => f.VariationForUser("a", false).VariationForUser("b", true).VariationForUser("c", false),
                booleanFlagBasicProps + ",\"targets\":[{\"variation\":0,\"values\":[\"b\"]}" +
                  ",{\"variation\":1,\"values\":[\"a\",\"c\"]}]"
                );
            VerifyFlag(
                f => f.VariationForUser("a", true).VariationForUser("b", true).VariationForUser("a", false),
                booleanFlagBasicProps + ",\"targets\":[{\"variation\":0,\"values\":[\"b\"]}" +
                  ",{\"variation\":1,\"values\":[\"a\"]}]"
                );

            string stringFlagBasicProps = "\"variations\":[\"red\",\"green\",\"blue\"],\"on\":true,\"rules\":[]"
                + ",\"offVariation\":0,\"fallthrough\":{\"variation\":2}";
            VerifyFlag(
                f => f.Variations(ThreeStringValues).OffVariation(0).FallthroughVariation(2)
                    .VariationForUser("a", 2).VariationForUser("b", 2),
                stringFlagBasicProps + ",\"targets\":[{\"variation\":2,\"values\":[\"a\",\"b\"]}]"
                );
            VerifyFlag(
                f => f.Variations(ThreeStringValues).OffVariation(0).FallthroughVariation(2)
                    .VariationForUser("a", 2).VariationForUser("b", 1).VariationForUser("c", 2),
                stringFlagBasicProps + ",\"targets\":[{\"variation\":1,\"values\":[\"b\"]}" +
                    ",{\"variation\":2,\"values\":[\"a\",\"c\"]}]"
                );
        }

        [Fact]
        public void FlagRules()
        {
            string basicProps = "\"variations\":[true,false],\"targets\":[]" +
                ",\"on\":true,\"offVariation\":1,\"fallthrough\":{\"variation\":0}";

            // match that returns variation 0/true
            string matchReturnsVariation0 = basicProps +
                ",\"rules\":[{\"id\":\"rule0\",\"variation\":0,\"trackEvents\":false,\"clauses\":[" +
                "{\"attribute\":\"name\",\"op\":\"in\",\"values\":[\"Lucy\"],\"negate\":false}" +
                "]}]";
            VerifyFlag(
                f => f.IfMatch(UserAttribute.Name, LdValue.Of("Lucy")).ThenReturn(true),
                matchReturnsVariation0
                );
            VerifyFlag(
                f => f.IfMatch(UserAttribute.Name, LdValue.Of("Lucy")).ThenReturn(0),
                matchReturnsVariation0
                );

            // match that returns variation 1/false
            string matchReturnsVariation1 = basicProps +
                ",\"rules\":[{\"id\":\"rule0\",\"variation\":1,\"trackEvents\":false,\"clauses\":[" +
                "{\"attribute\":\"name\",\"op\":\"in\",\"values\":[\"Lucy\"],\"negate\":false}" +
                "]}]";
            VerifyFlag(
                f => f.IfMatch(UserAttribute.Name, LdValue.Of("Lucy")).ThenReturn(false),
                matchReturnsVariation1
                );
            VerifyFlag(
                f => f.IfMatch(UserAttribute.Name, LdValue.Of("Lucy")).ThenReturn(1),
                matchReturnsVariation1
                );

            // negated match
            VerifyFlag(
                f => f.IfNotMatch(UserAttribute.Name, LdValue.Of("Lucy")).ThenReturn(true),
                basicProps + ",\"rules\":[{\"id\":\"rule0\",\"variation\":0,\"trackEvents\":false,\"clauses\":[" +
                    "{\"attribute\":\"name\",\"op\":\"in\",\"values\":[\"Lucy\"],\"negate\":true}" +
                    "]}]"
                );

            // multiple clauses
            VerifyFlag(
                f => f.IfMatch(UserAttribute.Name, LdValue.Of("Lucy"))
                    .AndMatch(UserAttribute.Country, LdValue.Of("gb"))
                    .ThenReturn(true),
                basicProps + ",\"rules\":[{\"id\":\"rule0\",\"variation\":0,\"trackEvents\":false,\"clauses\":[" +
                    "{\"attribute\":\"name\",\"op\":\"in\",\"values\":[\"Lucy\"],\"negate\":false}," +
                    "{\"attribute\":\"country\",\"op\":\"in\",\"values\":[\"gb\"],\"negate\":false}" +
                    "]}]"
                );

            // multiple rules
            VerifyFlag(
                f => f.IfMatch(UserAttribute.Name, LdValue.Of("Lucy")).ThenReturn(true)
                    .IfMatch(UserAttribute.Name, LdValue.Of("Mina")).ThenReturn(true),
                basicProps + ",\"rules\":["
                  + "{\"id\":\"rule0\",\"variation\":0,\"trackEvents\":false,\"clauses\":[" +
                    "{\"attribute\":\"name\",\"op\":\"in\",\"values\":[\"Lucy\"],\"negate\":false}" +
                    "]},"
                  + "{\"id\":\"rule1\",\"variation\":0,\"trackEvents\":false,\"clauses\":[" +
                    "{\"attribute\":\"name\",\"op\":\"in\",\"values\":[\"Mina\"],\"negate\":false}" +
                    "]}"
                  + "]"
                );
        }

        private void CreateAndStart()
        {
            var ds = _td.CreateDataSource(BasicContext, _updates);
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

        private void VerifyFlag(Func<TestData.FlagBuilder, TestData.FlagBuilder> configureFlag, string expectedProps)
        {
            var expectedJson = "{\"key\":\"flagkey\",\"version\":1," + expectedProps +
                ",\"clientSide\":false,\"deleted\":false,\"prerequisites\":[],\"salt\":\"\"}";

            var td = TestData.DataSource();
            td.CreateDataSource(BasicContext, _updates).Start();

            td.Update(configureFlag(_td.Flag("flagkey")));

            var up = _updates.Upserts.ExpectValue();
            AssertJsonEqual(expectedJson, DataModel.Features.Serialize(up.Item));
        }
    }
}
