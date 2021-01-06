using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

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
            var flag = MustParseFlag(expectedJson.ToJsonString());
            AssertFlagHasAllProperties(flag);
            var s = DataModel.Features.Serialize(new ItemDescriptor(flag.Version, flag));
            AssertHelpers.JsonEqual(expectedJson, LdValue.Parse(s));
        }

        [Fact]
        public void SerializeAndDeserializeSegment()
        {
            var expectedJson = SegmentWithAllPropertiesJson();
            var segment = MustParseSegment(expectedJson.ToJsonString());
            AssertSegmentHasAllProperties(segment);
            var s = DataModel.Segments.Serialize(new ItemDescriptor(segment.Version, segment));
            AssertHelpers.JsonEqual(expectedJson, LdValue.Parse(s));
        }

        [Fact]
        public void SerializeDeletedItems()
        {
            // It's important that the SDK provides a placeholder JSON object for deleted items, because some
            // of our existing database integrations aren't able to store the version number separately from
            // the JSON data.
            var deletedItem = ItemDescriptor.Deleted(2);
            var expected = LdValue.BuildObject().Add("version", 2).Add("deleted", true).Build();

            var s1 = DataModel.Features.Serialize(deletedItem);
            AssertHelpers.JsonEqual(expected, LdValue.Parse(s1));

            var s2 = DataModel.Segments.Serialize(deletedItem);
            AssertHelpers.JsonEqual(expected, LdValue.Parse(s2));                
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

        private LdValue FlagWithAllPropertiesJson()
        {
            return LdValue.Parse(@"{
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
                    { ""variation"": 2, ""weight"": 100000 }
                ],
                ""bucketBy"": ""email""
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
}");
        }

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
                            Assert.Equal("in", c.Op);
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
                            Assert.Equal(100000, v.Weight);
                        });
                    Assert.Equal(UserAttribute.Email, r.Rollout.Value.BucketBy);
                    Assert.Collection(r.Clauses);
                });

            Assert.NotNull(flag.Fallthrough);
            Assert.Equal(1, flag.Fallthrough.Variation);
            Assert.Null(flag.Fallthrough.Rollout);
            Assert.Equal(2, flag.OffVariation);
            Assert.Equal(ImmutableList.Create(LdValue.Of("a"), LdValue.Of("b"), LdValue.Of("c")), flag.Variations);
            Assert.True(flag.ClientSide);
            Assert.True(flag.TrackEvents);
            Assert.True(flag.TrackEventsFallthrough);
            Assert.Equal(UnixMillisecondTime.OfMillis(1000), flag.DebugEventsUntilDate);
        }

        private LdValue SegmentWithAllPropertiesJson()
        {
            return LdValue.Parse(@"{
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
    ]
}");
        }

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
                            Assert.Equal("in", c.Op);
                            Assert.Equal(new List<LdValue> { LdValue.Of("Lucy"), LdValue.Of("Mina") }, c.Values);
                            Assert.True(c.Negate);
                        });
                },
                r =>
                {
                    Assert.Null(r.Weight);
                    Assert.Null(r.BucketBy);
                    Assert.Collection(r.Clauses);
                });
        }
    }
}
