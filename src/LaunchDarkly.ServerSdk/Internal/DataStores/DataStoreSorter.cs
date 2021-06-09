using System;
using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    /// <summary>
    /// Uses a dependency graph to determine the preferred ordering for feature flag updates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some persistent data store implementations may not support atomic updates. In that case,
    /// it is desirable to add or update items in an order that will minimize the chance of an
    /// invalid intermediate state of the overall data set: for instance, if flag A has flag B
    /// as a prerequisite, then B should be added/updated before A.
    /// </para>
    /// </remarks>
    internal abstract class DataStoreSorter
    {
        public static FullDataSet<ItemDescriptor> SortAllCollections(FullDataSet<ItemDescriptor> allData)
        {
            var dataOut = new SortedDictionary<DataKind, KeyedItems<ItemDescriptor>>(
                PriorityComparer.Instance);
            foreach (var entry in allData.Data)
            {
                var kind = entry.Key;
                dataOut.Add(kind, new KeyedItems<ItemDescriptor>(SortCollection(kind, entry.Value.Items)));
            }
            return new FullDataSet<ItemDescriptor>(dataOut);
        }

        private static IEnumerable<KeyValuePair<string, ItemDescriptor>> SortCollection(DataKind kind,
            IEnumerable<KeyValuePair<string, ItemDescriptor>> input)
        {
            var dependencyKeysFn = GetDependenciesFunction(kind);
            if (dependencyKeysFn is null)
            {
                return input;
            }

            IDictionary<string, ItemDescriptor> remainingItems =
                input.ToDictionary(kv => kv.Key, kv => kv.Value);
            var outputOrdering = new OutputOrdering();

            while (remainingItems.Count > 0)
            {
                // pick a random item that hasn't been updated yet
                var entry = remainingItems.First();
                AddWithDependenciesFirst(entry.Key, entry.Value, remainingItems, dependencyKeysFn, outputOrdering);
            }

            var ret = new SortedDictionary<string, ItemDescriptor>(outputOrdering);
            foreach (var kv in input)
            {
                ret[kv.Key] = kv.Value;
            }
            return ret;
        }
        
        private static int GetDataKindOrdering(DataKind kind)
        {
            if (kind == DataModel.Features)
            {
                return 2;
            }
            else if (kind == DataModel.Segments)
            {
                return 1;
            }
            return 0;
        }

        private static Func<object, IEnumerable<string>> GetDependenciesFunction(DataKind kind)
        {
            if (kind == DataModel.Features)
            {
                return o =>
                {
                    if (o is FeatureFlag f && f.Prerequisites != null)
                    {
                        return from p in f.Prerequisites select p.Key;
                    }
                    return Enumerable.Empty<string>();
                };
            }
            return null;
        }

        private static void AddWithDependenciesFirst(string key, ItemDescriptor item,
            IDictionary<string, ItemDescriptor> remainingItems,
            Func<object, IEnumerable<string>> dependencyKeysFn,
            OutputOrdering output)
        {
            remainingItems.Remove(key);  // we won't need to visit this item again
            foreach (var prereqKey in dependencyKeysFn(item.Item))
            {
                if (remainingItems.TryGetValue(prereqKey, out var prereqItem))
                {
                    AddWithDependenciesFirst(prereqKey, prereqItem, remainingItems, dependencyKeysFn, output);
                }
            }
            output.Add(key);
        }

        private class PriorityComparer : IComparer<DataKind>
        {
            internal static readonly PriorityComparer Instance = new PriorityComparer();

            public int Compare(DataKind i1, DataKind i2)
            {
                return GetDataKindOrdering(i1) - GetDataKindOrdering(i2);
            }

        }

        // This Comparer is necessary because .NET Standard 1.x doesn't support OrderedDictionary,
        // so we need to use SortedDictionary instead.
        private class OutputOrdering : IComparer<string>
        {
            private readonly IDictionary<string, int> _ordering = new Dictionary<string, int>();
            private int _index;

            internal void Add(string key)
            {
                _ordering[key] = _index++;
            }

            public int Compare(string key1, string key2)
            {
                _ordering.TryGetValue(key1, out var o1);
                _ordering.TryGetValue(key2, out var o2);
                return o1 - o2;
            }
        }
    }
}
