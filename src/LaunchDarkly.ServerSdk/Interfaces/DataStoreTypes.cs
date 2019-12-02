using System;
using System.Collections.Generic;
using System.Linq;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    public static class DataStoreTypes
    {
        public class DataKind
        {
            private readonly string _name;
            private readonly Func<object, string> _serializer;
            private readonly Func<string, object> _deserializer;

            public string Name => _name;

            public string Serialize(object o) => _serializer(o);

            public object Deserialize(string serializedData) => _deserializer(serializedData);

            public DataKind(string name, Func<object, string> serializer,
                Func<string, object> deserializer)
            {
                _name = name;
                _serializer = serializer;
                _deserializer = deserializer;
            }

            public override string ToString()
            {
                return "DataKind(" + _name + ")";
            }
        }
        
        public struct ItemDescriptor
        {
            private readonly int _version;
            private readonly object _item;

            public int Version => _version;
            public object Item => _item;

            public ItemDescriptor(int version, object item)
            {
                _version = version;
                _item = item;
            }

            public static ItemDescriptor Deleted(int version) => new ItemDescriptor(version, null);

            /// <inheritdoc/>
            public override string ToString() => "ItemDescriptor(" + Version + "," + Item;
        }

        public struct SerializedItemDescriptor
        {
            private readonly int _version;
            private readonly string _serializedItem;

            public int Version => _version;
            public string SerializedItem => _serializedItem;

            public SerializedItemDescriptor(int version, string serializedItem)
            {
                _version = version;
                _serializedItem = serializedItem;
            }

            public static SerializedItemDescriptor Deleted(int version) =>
                new SerializedItemDescriptor(version, null);

            /// <inheritdoc/>
            public override string ToString() => "SerializedItemDescriptor(" + Version + "," + SerializedItem;
        }

        public struct FullDataSet<TDescriptor>
        {
            private readonly IEnumerable<KeyValuePair<DataKind, IEnumerable<KeyValuePair<string, TDescriptor>>>> _data;

            public IEnumerable<KeyValuePair<DataKind, IEnumerable<KeyValuePair<string, TDescriptor>>>> Data => _data;

            public FullDataSet(IEnumerable<KeyValuePair<DataKind, IEnumerable<KeyValuePair<string, TDescriptor>>>> data)
            {
                _data = data;
            }

            public static FullDataSet<TDescriptor> Empty() => new FullDataSet<TDescriptor>(
                Enumerable.Empty<KeyValuePair<DataKind, IEnumerable<KeyValuePair<string, TDescriptor>>>>());
        }
    }
}
