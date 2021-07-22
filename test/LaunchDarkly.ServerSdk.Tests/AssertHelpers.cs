using System.Collections.Generic;
using System.Linq;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server
{
    public static class AssertHelpers
    {
        public static void FullyEqual<T>(T a, T b)
        {
            Assert.Equal(a, b);
            Assert.Equal(b, a);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        public static void FullyUnequal<T>(T a, T b)
        {
            Assert.NotEqual(a, b);
            Assert.NotEqual(b, a);
        }

        public static void JsonEqual(string expected, string actual) =>
            JsonEqual(LdValue.Parse(expected), LdValue.Parse(actual));

        public static void JsonEqual(LdValue expected, LdValue actual)
        {
            if (!expected.Equals(actual))
            {
                var diff = DescribeJsonDifference(expected, actual, "");
                if (diff is null)
                {
                    Assert.Equal(expected, actual); // generates standard failure message
                }
                Assert.True(false, "JSON result mismatch:\n" + DescribeJsonDifference(expected, actual, ""));
            }
        }

        public static void DataSetsEqual(FullDataSet<ItemDescriptor> expected, FullDataSet<ItemDescriptor> actual)
        {
            var expectedNorm = TestUtils.NormalizeDataSet(expected);
            var actualNorm = TestUtils.NormalizeDataSet(actual);
            var ok = false;
            string expectedDesc = null, actualDesc = null;
            if (expectedNorm.Data.Count() == actualNorm.Data.Count())
            {
                ok = true;
                for (var i = 0; ok && i < expectedNorm.Data.Count(); i++)
                {
                    var ec = expectedNorm.Data.ElementAt(i);
                    var ac = actualNorm.Data.ElementAt(i);
                    if (ac.Key != ec.Key)
                    {
                        ok = false;
                    }
                    else
                    {
                        var kind = ac.Key;
                        for (var j = 0; ok && j < ec.Value.Items.Count(); j++)
                        {
                            var ei = ec.Value.Items.ElementAt(j);
                            var ai = ac.Value.Items.ElementAt(j);
                            if (ai.Key != ei.Key || !ItemsEqual(kind, ei.Value, ai.Value))
                            {
                                expectedDesc = DescribeDataCollection(kind, ei);
                                actualDesc = DescribeDataCollection(kind, ai);
                                ok = false;
                            }
                        }
                    }
                }
            }
            if (!ok)
            {
                if (expectedDesc is null)
                {
                    expectedDesc = DescribeDataSet(expectedNorm);
                    actualDesc = DescribeDataSet(actualNorm);
                }
                Assert.True(false, string.Format("data set mismatch:\nexpected: {0}\nactual: {1}",
                    expectedDesc, actualDesc));
            }
        }

        public static void DataItemsEqual(DataKind kind, ItemDescriptor expected, ItemDescriptor actual)
        {
            if (!ItemsEqual(kind, expected, actual))
            {
                Assert.True(false, string.Format("expected: {0}\nactual: {1}",
                    kind.Serialize(expected), kind.Serialize(actual)));
            }
        }

        private static string DescribeJsonDifference(LdValue expected, LdValue actual, string prefix)
        {
            if (expected.Type == LdValueType.Object && actual.Type == LdValueType.Object)
            {
                return DescribeJsonObjectDifference(expected, actual, prefix);
            }
            if (expected.Type == LdValueType.Array && actual.Type == LdValueType.Array)
            {
                return DescribeJsonArrayDifference(expected, actual, prefix);
            }
            return null;
        }

        private static string DescribeJsonObjectDifference(LdValue expected, LdValue actual, string prefix)
        {
            var expectedDict = expected.AsDictionary(LdValue.Convert.Json);
            var actualDict = actual.AsDictionary(LdValue.Convert.Json);
            var allKeys = expectedDict.Keys.Union(actualDict.Keys);
            var lines = new List<string>();
            foreach (var key in allKeys)
            {
                var prefixedKey = prefix + (prefix == "" ? "" : ".") + key;
                string expectedDesc = null, actualDesc = null, detailDiff = null;
                if (expectedDict.ContainsKey(key))
                {
                    if (actualDict.ContainsKey(key))
                    {
                        LdValue expectedProp = expectedDict[key], actualProp = actualDict[key];
                        if (expectedProp != actualProp)
                        {
                            expectedDesc = expectedProp.ToJsonString();
                            actualDesc = actualProp.ToJsonString();
                            detailDiff = DescribeJsonDifference(expectedProp, actualProp, prefixedKey);
                        }
                    }
                    else
                    {
                        expectedDesc = expectedDict[key].ToJsonString();
                        actualDesc = "<absent>";
                    }
                }
                else
                {
                    actualDesc = actualDict[key].ToJsonString();
                    expectedDesc = "<absent>";
                }
                if (expectedDesc != null || actualDesc != null)
                {
                    if (detailDiff != null)
                    {
                        lines.Add(detailDiff);
                    }
                    else
                    {
                        lines.Add(string.Format("property \"{0}\": expected = {1}, actual = {2}",
                            prefixedKey, expectedDesc, actualDesc));
                    }
                }
            }
            return string.Join("\n", lines);
        }

        private static string DescribeJsonArrayDifference(LdValue expected, LdValue actual, string prefix)
        {
            if (expected.Count != actual.Count)
            {
                return null; // can't provide a detailed diff, just show the whole values
            }
            var lines = new List<string>();
            for (var i = 0; i < expected.Count; i++)
            {
                var prefixedIndex = string.Format("{0}[{1}]", prefix, i);
                LdValue expectedElement = expected.Get(i), actualElement = actual.Get(i);
                if (actualElement != expectedElement)
                {
                    var detailDiff = DescribeJsonDifference(expectedElement, actualElement, prefixedIndex);
                    if (detailDiff != null)
                    {
                        lines.Add(detailDiff);
                    }
                    else
                    {
                        lines.Add(string.Format("property \"{0}\": expected = {1}, actual = {2}",
                            prefixedIndex, expectedElement, actualElement));
                    }
                }
            }
            return string.Join("\n", lines);
        }

        private static string DescribeDataSet(FullDataSet<ItemDescriptor> allData) =>
            string.Join(", ", allData.Data.Select(coll =>
                DescribeDataCollection(coll.Key, coll.Value.Items.ToArray())));

        private static string DescribeDataCollection(DataKind kind, params KeyValuePair<string, ItemDescriptor>[] items) =>
            kind.Name.ToUpper() + ": [" +
                string.Join(",", items.Select(keyedItem =>
                    "[" + keyedItem.Key + ": " + kind.Serialize(keyedItem.Value)))
                + "]";

        private class KeyedItemEqualityComparer : IEqualityComparer<KeyValuePair<string, ItemDescriptor>>
        {
            public DataKind Kind { get; set; }

            public bool Equals(KeyValuePair<string, ItemDescriptor> x, KeyValuePair<string, ItemDescriptor> y) =>
                x.Key == y.Key && ItemsEqual(Kind, x.Value, y.Value);

            public int GetHashCode(KeyValuePair<string, ItemDescriptor> obj) => 0;
        }

        private static bool ItemsEqual(DataKind kind, ItemDescriptor expected, ItemDescriptor actual) =>
            LdValue.Parse(kind.Serialize(actual)) == LdValue.Parse(kind.Serialize(expected));
    }
}
