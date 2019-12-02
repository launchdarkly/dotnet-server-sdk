using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    internal abstract class DataStoreDataSetSorter
    {
        public static IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> SortAllCollections(
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData)
        {
            var dataOut = new SortedDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>(
                PriorityComparer.Instance);
            foreach (var entry in allData)
            {
                var kind = entry.Key;
                dataOut.Add(kind, SortCollection(kind, entry.Value));
            }
            return dataOut;
        }

        private static IDictionary<string, IVersionedData> SortCollection(IVersionedDataKind kind,
            IDictionary<string, IVersionedData> input)
        {
            if (!(kind is IVersionedDataOrdering ordering))
            {
                return input;
            }

            IDictionary<string, IVersionedData> remainingItems =
                new Dictionary<string, IVersionedData>(input);
            var outputOrdering = new OutputOrdering();

            while (remainingItems.Count > 0)
            {
                // pick a random item that hasn't been updated yet
                foreach (var entry in remainingItems)
                {
                    AddWithDependenciesFirst(entry.Value, remainingItems, ordering, outputOrdering);
                    break;
                }
            }

            return new SortedDictionary<string, IVersionedData>(input, outputOrdering);
        }

        private static void AddWithDependenciesFirst(IVersionedData item,
            IDictionary<string, IVersionedData> remainingItems,
            IVersionedDataOrdering ordering,
            OutputOrdering output)
        {
            remainingItems.Remove(item.Key);  // we won't need to visit this item again
            foreach (var prereqKey in ordering.GetDependencyKeys(item))
            {
                if (remainingItems.TryGetValue(prereqKey, out var prereqItem))
                {
                    AddWithDependenciesFirst(prereqItem, remainingItems, ordering, output);
                }
            }
            output.Add(item);
        }

        private class PriorityComparer : IComparer<IVersionedDataKind>
        {
            internal static readonly PriorityComparer Instance = new PriorityComparer();

            public int Compare(IVersionedDataKind i1, IVersionedDataKind i2)
            {
                var p1 = (i1 as IVersionedDataOrdering)?.Priority ?? 0;
                var p2 = (i2 as IVersionedDataOrdering)?.Priority ?? 0;
                return p1 - p2;
            }
        }

        // This Comparer is necessary because .NET Standard 1.x doesn't support OrderedDictionary,
        // so we need to use SortedDictionary instead.
        private class OutputOrdering : IComparer<string>
        {
            private readonly IDictionary<string, int> _ordering = new Dictionary<string, int>();
            private int _index;

            internal void Add(IVersionedData item)
            {
                _ordering[item.Key] = _index++;
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
