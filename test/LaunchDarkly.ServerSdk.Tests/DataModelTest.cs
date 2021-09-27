using System.Collections.Generic;
using System.Collections.Immutable;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using static LaunchDarkly.TestHelpers.JsonAssertions;

namespace LaunchDarkly.Sdk.Server
{
    public class DataModelTest
    {
        [Fact]
        public void DataKindNames()
        {
            Assert.Equal("features", DataModel.Features.Name);
            Assert.Equal("DataKind(features)", DataModel.Features.ToString());

            Assert.Equal("segments", DataModel.Segments.Name);
            Assert.Equal("DataKind(segments)", DataModel.Segments.ToString());
        }

        [Fact]
        public void SerializeAndDeserializeFlag()
        {
            var expectedJson = FlagWithAllPropertiesJson();
            var flag = MustParseFlag(expectedJson);
            AssertFlagHasAllProperties(flag);
            var s = DataModel.Features.Serialize(new ItemDescriptor(flag.Version, flag));
            AssertJsonEqual(expectedJson, s);
        }

        [Fact]
        public void SerializeAndDeserializeSegment()
        {
            var expectedJson = SegmentWithAllPropertiesJson();
            var segment = MustParseSegment(expectedJson);
            AssertSegmentHasAllProperties(segment);
            var s = DataModel.Segments.Serialize(new ItemDescriptor(segment.Version, segment));
            AssertJsonEqual(expectedJson, s);
        }

        [Fact]
        public void SerializeDeletedItems()
        {
            // It's important that the SDK provides a placeholder JSON object for deleted items, because some
            // of our existing database integrations aren't able to store the version number separately from
            // the JSON data.
            var deletedItem = ItemDescriptor.Deleted(2);
            var expected = LdValue.BuildObject().Add("version", 2).Add("deleted", true).Build().ToJsonString();

            var s1 = DataModel.Features.Serialize(deletedItem);
            AssertJsonEqual(expected, s1);

            var s2 = DataModel.Segments.Serialize(deletedItem);
            AssertJsonEqual(expected, s2);
        }

        [Fact]
        public void DeserializeDeletedItems()
        {
            var json = LdValue.BuildObject().Add("version", 2).Add("deleted", true).Build().ToJsonString();

            var item1 = DataModel.Features.Deserialize(json);
            Assert.Equal(2, item1.Version);
            Assert.Null(item1.Item);

            var item2 = DataModel.Features.Deserialize(json);
            Assert.Equal(2, item2.Version);
            Assert.Null(item2.Item);
        }

        [Fact]
        public void CollectionsAreNeverNull()
        {
            var flag = MustParseFlag(@"{""key"":""flagkey"",""version"":1}");
            Assert.NotNull(flag.Prerequisites);
            Assert.NotNull(flag.Rules);
            Assert.NotNull(flag.Targets);

            var flagWithRule = MustParseFlag(@"{""key"":""flagkey"",""version"":1,""rules"":[ {} ]}");
            Assert.Collection(flagWithRule.Rules, r => Assert.NotNull(r.Clauses));

            var segment = MustParseSegment(@"{""key"":""segmentkey"",""version"":1}");
            Assert.NotNull(segment.Rules);

            var segmentWithRule = MustParseSegment(@"{""key"":""segmentkey"",""version"":1,""rules"":[ { } ]}");
            Assert.Collection(segmentWithRule.Rules, r => Assert.NotNull(r.Clauses));
        }
        
        [Fact]
        public void OptionalFlagPropertiesAreNullable()
        {
            var flag1 = MustParseFlag(@"{
                ""key"": ""flag-key"",
                ""version"": 99,
                ""salt"": null
            }");
            Assert.Null(flag1.Salt);

            var flag2 = MustParseFlag(@"{
                ""key"": ""flag-key"",
                ""version"": 99,
                ""rules"": [ { ""id"": null } ]
            }");
            Assert.Collection(flag2.Rules, r => Assert.Null(r.Id));

            // Null rollout is the same as omitting the rollout
            var flag3 = MustParseFlag(@"{
                ""key"": ""flag-key"",
                ""version"": 99,
                ""fallthrough"": { ""variation"": 0, ""rollout"": null }
            }");
            Assert.Null(flag3.Fallthrough.Rollout);

            // Null VariationOrRollout isn't really valid, evaluation will fail (you're supposed to
            // have either a variation or a rollout), but we should still be able to parse the flag.
            var flag4 = MustParseFlag(@"{
                ""key"": ""flag-key"",
                ""version"": 99,
                ""fallthrough"": null
            }");
            Assert.Null(flag4.Fallthrough.Variation);
            Assert.Null(flag4.Fallthrough.Rollout);

            // Rollout bucketBy and seed are nullable
            var flag5 = MustParseFlag(@"{
                ""key"": ""flag-key"",
                ""version"": 99,
                ""fallthrough"": {
                    ""rollout"": {
                        ""bucketBy"": null,
                        ""seed"": null,
                        ""variations"": [ { ""variation"": 0, ""weight"": 100000 } ]
                    }
                }
            }");
            Assert.Null(flag5.Fallthrough.Rollout.Value.BucketBy);
            Assert.Null(flag5.Fallthrough.Rollout.Value.Seed);
            Assert.Equal(RolloutKind.Rollout, flag5.Fallthrough.Rollout.Value.Kind); // default value
        }

        [Fact]
        public void OptionalSegmentStringPropertiesAreNullable()
        {
            var segment1 = MustParseSegment(@"{
                ""key"": ""segment-key"",
                ""version"": 99,
                ""salt"": null
            }");
            Assert.Null(segment1.Salt);
        }

        private FeatureFlag MustParseFlag(string json)
        {
            var item = DataModel.Features.Deserialize(json);
            var flag = Assert.IsType<FeatureFlag>(item.Item);
            Assert.Equal(flag.Version, item.Version);
            return flag;
        }

        private Segment MustParseSegment(string json)
        {
            var item = DataModel.Segments.Deserialize(json);
            var segment = Assert.IsType<Segment>(item.Item);
            Assert.Equal(segment.Version, item.Version);
            return segment;
        }

        private string FlagWithAllPropertiesJson() => @"{
    ""key"": ""flag-key"",
    ""version"": 99,
    ""deleted"": false,
    ""on"": true,
    ""prerequisites"": [
        { ""key"": ""prereqkey"", ""variation"": 3 }
    ],
    ""salt"": ""123"",
    ""targets"": [
        { ""variation"": 1, ""values"": [""key1"", ""key2""] }
    ],
    ""rules"": [
        {
            ""id"": ""id0"",
            ""variation"": 2,
            ""clauses"": [
                {
                    ""attribute"": ""name"",
                    ""op"": ""in"",
                    ""values"": [ ""Lucy"", ""Mina"" ],
                    ""negate"": true
                }
            ],
            ""trackEvents"": true
        },
        {
            ""id"": ""id1"",
            ""rollout"": {
                ""variations"": [
                    { ""variation"": 2, ""weight"": 40000 },
                    { ""variation"": 1, ""weight"": 60000, ""untracked"": true }
                ],
                ""bucketBy"": ""email"",
                ""kind"": ""experiment"",
                ""seed"": 123
            },
            ""clauses"": [],
            ""trackEvents"": false
        }
    ],
    ""fallthrough"": { ""variation"": 1 },
    ""offVariation"": 2,
    ""variations"": [""a"", ""b"", ""c""],
    ""clientSide"": true,
    ""trackEvents"": true,
    ""trackEventsFallthrough"": true,
    ""debugEventsUntilDate"": 1000
}";

        private void AssertFlagHasAllProperties(FeatureFlag flag)
        {
            Assert.Equal("flag-key", flag.Key);
            Assert.Equal(99, flag.Version);
            Assert.True(flag.On);
            Assert.Equal("123", flag.Salt);

            Assert.Collection(flag.Prerequisites,
                p =>
                {
                    Assert.Equal("prereqkey", p.Key);
                    Assert.Equal(3, p.Variation);
                });

            Assert.Collection(flag.Targets,
                t =>
                {
                    Assert.Equal(1, t.Variation);
                    Assert.Equal(ImmutableList.Create("key1", "key2"), t.Values);
                });

            Assert.Collection(flag.Rules,
                r =>
                {
                    Assert.Equal("id0", r.Id);
                    Assert.True(r.TrackEvents);
                    Assert.Equal(2, r.Variation);
                    Assert.Null(r.Rollout);
                    Assert.Collection(r.Clauses,
                        c =>
                        {
                            Assert.Equal(UserAttribute.Name, c.Attribute);
                            Assert.Equal(Operator.In, c.Op);
                            Assert.Equal(ImmutableList.Create(LdValue.Of("Lucy"), LdValue.Of("Mina")), c.Values);
                            Assert.True(c.Negate);
                        });
                },
                r =>
                {
                    Assert.Equal("id1", r.Id);
                    Assert.False(r.TrackEvents);
                    Assert.Null(r.Variation);
                    Assert.NotNull(r.Rollout);
                    Assert.Collection(r.Rollout.Value.Variations,
                        v =>
                        {
                            Assert.Equal(2, v.Variation);
                            Assert.Equal(40000, v.Weight);
                            Assert.False(v.Untracked);
                        },
                        v =>
                        {
                            Assert.Equal(1, v.Variation);
                            Assert.Equal(60000, v.Weight);
                            Assert.True(v.Untracked);
                        });
                    Assert.Equal(UserAttribute.Email, r.Rollout.Value.BucketBy);
                    Assert.Equal(RolloutKind.Experiment, r.Rollout.Value.Kind);
                    Assert.Equal(123, r.Rollout.Value.Seed);
                    Assert.Empty(r.Clauses);
                });

            Assert.Equal(1, flag.Fallthrough.Variation);
            Assert.Null(flag.Fallthrough.Rollout);
            Assert.Equal(2, flag.OffVariation);
            Assert.Equal(ImmutableList.Create(LdValue.Of("a"), LdValue.Of("b"), LdValue.Of("c")), flag.Variations);
            Assert.True(flag.ClientSide);
            Assert.True(flag.TrackEvents);
            Assert.True(flag.TrackEventsFallthrough);
            Assert.Equal(UnixMillisecondTime.OfMillis(1000), flag.DebugEventsUntilDate);
        }

        private string SegmentWithAllPropertiesJson() => @"{
    ""key"": ""segment-key"",
    ""version"": 99,
    ""deleted"": false,
    ""included"": [""key1"", ""key2""],
    ""excluded"": [""key3"", ""key4""],
    ""salt"": ""123"",
    ""rules"": [
        {
            ""weight"": 50000,
            ""bucketBy"": ""email"",
            ""clauses"": [
                {
                    ""attribute"": ""name"",
                    ""op"": ""in"",
                    ""values"": [ ""Lucy"", ""Mina"" ],
                    ""negate"": true
                }
            ]
        },
        {
            ""clauses"": []
        }
    ],
    ""unbounded"": true,
    ""generation"": 51
}";

        private void AssertSegmentHasAllProperties(Segment segment)
        {
            Assert.Equal("segment-key", segment.Key);
            Assert.Equal(99, segment.Version);
            Assert.Equal("123", segment.Salt);
            Assert.Equal(ImmutableList.Create("key1", "key2"), segment.Included);
            Assert.Equal(ImmutableList.Create("key3", "key4"), segment.Excluded);
            
            Assert.Collection(segment.Rules,
                r =>
                {
                    Assert.Equal(50000, r.Weight);
                    Assert.Equal(UserAttribute.Email, r.BucketBy);
                    Assert.Collection(r.Clauses,
                        c =>
                        {
                            Assert.Equal(UserAttribute.Name, c.Attribute);
                            Assert.Equal(Operator.In, c.Op);
                            Assert.Equal(new List<LdValue> { LdValue.Of("Lucy"), LdValue.Of("Mina") }, c.Values);
                            Assert.True(c.Negate);
                        });
                },
                r =>
                {
                    Assert.Null(r.Weight);
                    Assert.Null(r.BucketBy);
                    Assert.Empty(r.Clauses);
                });

            Assert.True(segment.Unbounded);
            Assert.Equal(51, segment.Generation);
        }
    }
}
