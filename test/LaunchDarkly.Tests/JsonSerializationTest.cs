using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class JsonSerializationTest
    {
        // These tests verify that Newtonsoft.Json can correctly serialize and deserialize all
        // properties of the FeatureFlag and Segment classes. That's not used in this library,
        // but is a requirement for any JSON-and-reflection-based implementation of IFeatureStore.

        [Fact]
        public void CanSerializeAndDeserializeFeatureFlag()
        {
            var flag1 = BuildFlag();
            var jsonString1 = JsonConvert.SerializeObject(flag1);
            var json1 = JsonConvert.DeserializeObject<JToken>(jsonString1);
            AssertJsonEquals(BuildFlagJson(), json1);

            var flag2 = JsonConvert.DeserializeObject<FeatureFlag>(jsonString1);
            var jsonString2 = JsonConvert.SerializeObject(flag2);
            var json2 = JsonConvert.DeserializeObject<JToken>(jsonString2);
            AssertJsonEquals(BuildFlagJson(), json2);
        }

        [Fact]
        public void CanSerializeAndDeserializeSegment()
        {
            var seg1 = BuildSegment();
            var jsonString1 = JsonConvert.SerializeObject(seg1);
            var json1 = JsonConvert.DeserializeObject<JToken>(jsonString1);
            AssertJsonEquals(BuildSegmentJson(), json1);

            var seg2 = JsonConvert.DeserializeObject<Segment>(jsonString1);
            var jsonString2 = JsonConvert.SerializeObject(seg2);
            var json2 = JsonConvert.DeserializeObject<JToken>(jsonString2);
            AssertJsonEquals(BuildSegmentJson(), json2);
        }

        private void AssertJsonEquals(JToken expected, JToken actual)
        {
            if (!JToken.DeepEquals(actual, expected))
            {
                Assert.True(false, "Expected " + JsonConvert.SerializeObject(expected) +
                    ", got " + JsonConvert.SerializeObject(actual));
            }
        }

        private FeatureFlag BuildFlag()
        {
            var clause = new Clause("name", "in", new List<JValue> { new JValue("x") }, true);
            var wv = new WeightedVariation(0, 50);
            var rollout = new Rollout(new List<WeightedVariation> { wv }, "key");
            var rule = new Rule("ruleid", 0, rollout, new List<Clause> { clause });
            var target = new Target(new List<string> { "userkey" }, 0);
            return new FeatureFlagBuilder("flagkey")
                .DebugEventsUntilDate(100000)
                .Deleted(true)
                .Fallthrough(new VariationOrRollout(0, rollout))
                .OffVariation(0)
                .On(true)
                .Prerequisites(new List<Prerequisite> { new Prerequisite("prereq", 1) })
                .Rules(new List<Rule> { rule })
                .Salt("NaCl")
                .Targets(new List<Target> { target })
                .TrackEvents(true)
                .Variations(new List<JToken> { new JValue("value") })
                .Version(100)
                .Build();
        }

        private JToken BuildFlagJson()
        {
            return JsonConvert.DeserializeObject<JToken>(
                @"{
                    ""key"": ""flagkey"",
                    ""debugEventsUntilDate"": 100000,
                    ""deleted"": true,
                    ""fallthrough"": {
                        ""variation"": 0,
                        ""rollout"": {
                            ""variations"": [ { ""variation"": 0, ""weight"": 50 } ],
                            ""bucketBy"": ""key""
                        }
                    },
                    ""offVariation"": 0,
                    ""on"": true,
                    ""prerequisites"": [ { ""key"": ""prereq"", ""variation"": 1 } ],
                    ""rules"": [
                        {
                            ""id"": ""ruleid"",
                            ""variation"": 0,
                            ""rollout"": {
                                ""variations"": [ { ""variation"": 0, ""weight"": 50 } ],
                                ""bucketBy"": ""key""
                            },
                            ""clauses"": [
                                { ""attribute"": ""name"", ""op"": ""in"", ""values"": [ ""x"" ], ""negate"": true }
                            ]
                        }
                    ],
                    ""salt"": ""NaCl"",
                    ""targets"": [ { ""values"": [ ""userkey"" ], ""variation"": 0 } ],
                    ""trackEvents"": true,
                    ""variations"": [ ""value"" ],
                    ""version"": 100
                }"
            );
        }

        private Segment BuildSegment()
        {
            var clause = new Clause("name", "in", new List<JValue> { new JValue("x") }, true);
            var rule = new SegmentRule(new List<Clause> { clause }, 50, "key");
            return new Segment(
                "segkey",
                100,
                new List<string> { "includeme" },
                new List<string> { "excludeme" },
                "NaCl",
                new List<SegmentRule> { rule },
                true
            );
        }

        private JToken BuildSegmentJson()
        {
            return JsonConvert.DeserializeObject<JToken>(
                @"{
                    ""key"": ""segkey"",
                    ""deleted"": true,
                    ""excluded"": [ ""excludeme"" ],
                    ""included"": [ ""includeme"" ],
                    ""rules"": [
                        {
                            ""clauses"": [
                                { ""attribute"": ""name"", ""op"": ""in"", ""values"": [ ""x"" ], ""negate"": true }
                            ],
                            ""weight"": 50,
                            ""bucketBy"": ""key""
                        }
                    ],
                    ""salt"": ""NaCl"",
                    ""version"": 100
                }"
            );
        }
    }
}
