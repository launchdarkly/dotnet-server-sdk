using System.Collections.Generic;
using System.Collections.Immutable;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    public static class DataStoreTestTypes
    {
        internal class TestItem
        {
            private readonly string _name;

            public string Name => _name;

            public TestItem(string name)
            {
                _name = name;
            }

            public static TestItem Deserialize(string s) => new TestItem(s);

            public string Serialize() => Name;

            public ItemDescriptor WithVersion(int version) => new ItemDescriptor(version, this);

            public SerializedItemDescriptor SerializedWithVersion(int version) =>
                new SerializedItemDescriptor(version, Serialize());

            public override bool Equals(object o) => (o is TestItem other && other.Name == Name);

            public override int GetHashCode() => _name.GetHashCode();

            public override string ToString() => "TestItem(" + Name + ")";
        }

        internal static readonly DataKind TestDataKind = new DataKind("testdata",
            item => (item as TestItem).Serialize(),
            TestItem.Deserialize);

        internal static readonly DataKind OtherDataKind = new DataKind("otherdata",
            item => (item as TestItem).Name,
            TestItem.Deserialize);

        internal class TestDataBuilder
        {
            private readonly Dictionary<DataKind, Dictionary<string, ItemDescriptor>> _data =
                new Dictionary<DataKind, Dictionary<string, ItemDescriptor>>();

            public FullDataSet<ItemDescriptor> Build()
            {
                return new FullDataSet<ItemDescriptor>(_data.ToImmutableDictionary(kv => kv.Key,
                    kv => (IEnumerable<KeyValuePair<string, ItemDescriptor>>)kv.Value.ToImmutableDictionary()));
            }

            public TestDataBuilder Add(DataKind kind, string key, int version, object item)
            {
                if (!_data.ContainsKey(kind))
                {
                    _data[kind] = new Dictionary<string, ItemDescriptor>();
                }
                _data[kind][key] = new ItemDescriptor(version, item);
                return this;
            }
        }
    }
}
