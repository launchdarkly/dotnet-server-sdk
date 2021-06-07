using System.Collections.Generic;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.BigSegments
{
    internal struct MembershipBuilder
    {
        private bool _nonEmpty;
        private string _firstValue;
        private bool _firstValueIncluded;
        private SortedDictionary<string, bool> _dict;

        internal void AddRefs(IEnumerable<string> segmentRefs, bool included)
        {
            if (segmentRefs is null)
            {
                return;
            }
            foreach (var s in segmentRefs)
            {
                if (s is null)
                {
                    continue;
                }
                if (_nonEmpty)
                {
                    if (_dict is null)
                    {
                        _dict = new SortedDictionary<string, bool>();
                        _dict[_firstValue] = _firstValueIncluded;
                    }
                    _dict[s] = included;
                }
                else
                {
                    _firstValue = s;
                    _firstValueIncluded = included;
                    _nonEmpty = true;
                }
            }
        }

        internal IMembership Build()
        {
            if (_nonEmpty)
            {
                if (_dict != null)
                {
                    return new MembershipDictionaryImpl(_dict);
                }
                return new MembershipSingleValueImpl(_firstValue, _firstValueIncluded);
            }
            return EmptyMembership.Instance;
        }

        private sealed class EmptyMembership : IMembership
        {
            internal static readonly EmptyMembership Instance = new EmptyMembership();

            public bool? CheckMembership(string segmentRef) => null;

            public override bool Equals(object obj) => obj is EmptyMembership;

            public override int GetHashCode() => 0;
        }

        private sealed class MembershipSingleValueImpl : IMembership
        {
            private readonly string _segmentRef;
            private readonly bool _included;

            internal MembershipSingleValueImpl(string segmentRef, bool included)
            {
                _segmentRef = segmentRef;
                _included = included;
            }

            public bool? CheckMembership(string segmentRef) =>
                segmentRef == _segmentRef ? (bool?)_included : null;

            public override bool Equals(object obj) =>
                obj is MembershipSingleValueImpl o &&
                _segmentRef == o._segmentRef &&
                _included == o._included;

            public override int GetHashCode() =>
                _segmentRef.GetHashCode() + (_included ? 1 : 0);
        }

        private sealed class MembershipDictionaryImpl : IMembership
        {
            private readonly SortedDictionary<string, bool> _dict;

            internal MembershipDictionaryImpl(SortedDictionary<string, bool> dict)
            {
                _dict = dict;
            }

            public bool? CheckMembership(string segmentRef) =>
                _dict.TryGetValue(segmentRef, out var value) ? (bool?)value : null;

            public override bool Equals(object obj)
            {
                if (!(obj is MembershipDictionaryImpl o))
                {
                    return false;
                }
                if (_dict.Count != o._dict.Count)
                {
                    return false;
                }
                foreach (var kv in _dict)
                {
                    if (!o._dict.TryGetValue(kv.Key, out var value) || value != kv.Value)
                    {
                        return false;
                    }
                }
                return true;
            }

            public override int GetHashCode()
            {
                int ret = 0;
                foreach (var kv in _dict)
                {
                    ret = ret * 33 + kv.Key.GetHashCode() + (kv.Value ? 1 : 0);
                }
                return ret;
            }
        }
    }
}
