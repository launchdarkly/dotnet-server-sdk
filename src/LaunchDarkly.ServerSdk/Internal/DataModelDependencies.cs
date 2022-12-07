using System;
using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal readonly struct KindAndKey : IEquatable<KindAndKey>
    {
        public DataKind Kind { get; }
        public string Key { get; }

        internal KindAndKey(DataKind kind, string key)
        {
            Kind = kind;
            Key = key;
        }

        public bool Equals(KindAndKey other)
        {
            return Kind == other.Kind && Key == other.Key;
        }

        public override int GetHashCode()
        {
            return Kind.GetHashCode() * 17 + Key.GetHashCode();
        }
    }

    internal class DependencyTracker
    {
        private readonly Dictionary<KindAndKey, ISet<KindAndKey>> _dependenciesFrom;
        private readonly Dictionary<KindAndKey, ISet<KindAndKey>> _dependenciesTo;

        internal DependencyTracker()
        {
            _dependenciesFrom = new Dictionary<KindAndKey, ISet<KindAndKey>>();
            _dependenciesTo = new Dictionary<KindAndKey, ISet<KindAndKey>>();
        }

        internal void Clear()
        {
            _dependenciesFrom.Clear();
            _dependenciesTo.Clear();
        }

        internal void UpdateDependenciesFrom(
            DataKind kind,
            string fromKey,
            ItemDescriptor fromItem
            )
        {
            var fromWhat = new KindAndKey(kind, fromKey);
            var updatedDependencies = ComputeDependenciesFrom(kind, fromItem); // never null

            if (_dependenciesFrom.TryGetValue(fromWhat, out var oldDependencySet))
            {
                foreach (var oldDep in oldDependencySet)
                {
                    if (_dependenciesTo.TryGetValue(oldDep, out var depsToThisOldDep))
                    {
                        depsToThisOldDep.Remove(fromWhat);
                    }
                }
            }
            _dependenciesFrom[fromWhat] = updatedDependencies;
            foreach (var newDep in updatedDependencies)
            {
                if (!_dependenciesTo.TryGetValue(newDep, out var depsToThisNewDep))
                {
                    depsToThisNewDep = new HashSet<KindAndKey>();
                    _dependenciesTo[newDep] = depsToThisNewDep;
                }
                depsToThisNewDep.Add(fromWhat);
            }
        }

        internal void AddAffectedItems(ISet<KindAndKey> itemsOut, KindAndKey initialModifiedItem)
        {
            if (!itemsOut.Contains(initialModifiedItem))
            {
                itemsOut.Add(initialModifiedItem);
                if (_dependenciesTo.TryGetValue(initialModifiedItem, out var affectedItems))
                {
                    foreach (var affectedItem in affectedItems)
                    {
                        AddAffectedItems(itemsOut, affectedItem);
                    }
                }
            }
        }

        internal static ISet<KindAndKey> ComputeDependenciesFrom(DataKind fromKind, ItemDescriptor fromItem)
        {
            if (fromItem.Item is null)
            {
                return new HashSet<KindAndKey>();
            }
            if (fromKind == DataModel.Features)
            {
                var flag = fromItem.Item as FeatureFlag;
                var prereqFlagKeys = flag.Prerequisites.Select(p => p.Key);
                var segmentKeys = flag.Rules.SelectMany(rule => SegmentKeysFromClauses(rule.Clauses));
                return new HashSet<KindAndKey>(KindAndKeys(DataModel.Features, prereqFlagKeys)
                    .Union(KindAndKeys(DataModel.Segments, segmentKeys)));
            }
            else if (fromKind == DataModel.Segments)
            {
                var segment = fromItem.Item as Segment;
                var segmentKeys = segment.Rules.SelectMany(rule => SegmentKeysFromClauses(rule.Clauses));
                return new HashSet<KindAndKey>(KindAndKeys(DataModel.Segments, segmentKeys));
            }
            return new HashSet<KindAndKey>();
        }

        private static IEnumerable<string> SegmentKeysFromClauses(IEnumerable<Clause> clauses) =>
            clauses.SelectMany(clause =>
                        clause.Op == Operator.SegmentMatch ?
                            clause.Values.Select(v => v.AsString) :
                            Enumerable.Empty<string>()
                    );

        private static IEnumerable<KindAndKey> KindAndKeys(DataKind kind, IEnumerable<string> keys) =>
            keys.Select(key => new KindAndKey(kind, key));
    }
}
