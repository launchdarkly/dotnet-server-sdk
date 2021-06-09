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

            public static ItemDescriptor Deserialize(string s)
            {
                var parts = s.Split(':');
                var version = int.Parse(parts[1]);
                return new ItemDescriptor(version,
                    parts[0] == "DELETED" ? null : new TestItem(parts[0]));
            }

            public static string Serialize(ItemDescriptor item) =>
                (item.Item is null ? "DELETED" : (item.Item as TestItem).Name) +
                ":" + item.Version;

            public ItemDescriptor WithVersion(int version) => new ItemDescriptor(version, this);

            public SerializedItemDescriptor SerializedWithVersion(int version) =>
                new SerializedItemDescriptor(version, false,
                    Serialize(new ItemDescriptor(version, this)));

            public override bool Equals(object o) => (o is TestItem other && other.Name == Name);

            public override int GetHashCode() => _name.GetHashCode();

            public override string ToString() => "TestItem(" + Name + ")";
        }

        internal static readonly DataKind TestDataKind = new DataKind("testdata",
            TestItem.Serialize,
            TestItem.Deserialize);

        internal static readonly DataKind OtherDataKind = new DataKind("otherdata",
            TestItem.Serialize,
            TestItem.Deserialize);

        internal class TestDataBuilder
        {
            private readonly Dictionary<DataKind, Dictionary<string, ItemDescriptor>> _data =
                new Dictionary<DataKind, Dictionary<string, ItemDescriptor>>();

            public FullDataSet<ItemDescriptor> Build()
            {
                return new FullDataSet<ItemDescriptor>(_data.ToImmutableDictionary(kv => kv.Key,
                    kv => new KeyedItems<ItemDescriptor>(kv.Value.ToImmutableDictionary())));
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
