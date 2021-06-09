using System;
using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.JsonStream;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Types that are used by the <see cref="IDataStore"/> interface.
    /// </summary>
    public static class DataStoreTypes
    {
        /// <summary>
        /// Represents a separately namespaced collection of storable data items.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The SDK passes instances of this type to the data store to specify whether it is
        /// referring to a feature flag, a user segment, etc. The data store implementation
        /// should not look for a specific data kind (such as feature flags), but should treat
        /// all data kinds generically.
        /// </para>
        /// </remarks>
        public sealed class DataKind
        {
            internal delegate void SerializerToJWriter(object item, IValueWriter writer);
            internal delegate ItemDescriptor DeserializerFromJReader(ref JReader reader);

            private readonly string _name;
            private readonly Func<ItemDescriptor, string> _serializer;
            private readonly Func<string, ItemDescriptor> _deserializer;
            private readonly SerializerToJWriter _internalSerializer;
            private readonly DeserializerFromJReader _internalDeserializer;

            /// <summary>
            /// A case-sensitive alphabetic string that uniquely identifies this data kind.
            /// </summary>
            /// <remarks>
            /// This is in effect a namespace for a collection of items of the same kind. Item
            /// keys must be unique within that namespace. Persistent data store implementations
            /// could use this string as part of a composite key or table name.
            /// </remarks>
            public string Name => _name;

            /// <summary>
            /// Returns a serialized representation of an item of this kind.
            /// </summary>
            /// <remarks>
            /// The SDK uses this function to generate the data that is stored by an <see cref="IPersistentDataStore"/>.
            /// Store implementations normally do not need to call it, except in a special case described in the
            /// documentation for <see cref="IPersistentDataStore"/> regarding deleted item placeholders.
            /// </remarks>
            /// <param name="item">an <see cref="ItemDescriptor"/></param> describing the object to be serialized
            /// <returns>the serialized representation</returns>
            public string Serialize(ItemDescriptor item) => _serializer(item);

            /// <summary>
            /// Creates an item of this kind from its serialized representation.
            /// </summary>
            /// <remarks>
            /// <para>
            /// The SDK uses this function to translate data that is returned by an <see cref="IPersistentDataStore"/>.
            /// Store implementations normally do not need to call it, except in a special case described in the
            /// documentation for <see cref="IPersistentDataStore"/> regarding updates.
            /// </para>
            /// <para>
            /// The returned <see cref="ItemDescriptor"/> has two properties: <see cref="ItemDescriptor.Item"/>,
            /// which is the deserialized object <i>or</i> a <see langword="null"/> value for a deleted item
            /// placeholder, and <see cref="ItemDescriptor.Version"/>, which provides the object's version number
            /// regardless of whether it is deleted or not.
            /// </para>
            /// </remarks>
            /// <param name="serializedData">the serialized representation</param>
            /// <returns>an <see cref="ItemDescriptor"/> describing the deserialized object</returns>
            public ItemDescriptor Deserialize(string serializedData) => _deserializer(serializedData);

            internal void SerializeToJWriter(ItemDescriptor item, IValueWriter writer)
            {
                if (_internalSerializer is null)
                {
                    throw new ArgumentException("SDK tried to serialize a non-built-in data kind");
                }
                _internalSerializer(item.Item, writer);
            }

            internal ItemDescriptor DeserializeFromJReader(ref JReader reader)
            {
                if (_internalDeserializer is null)
                {
                    throw new ArgumentException("SDK tried to deserialize a non-built-in data kind");
                }
                return _internalDeserializer(ref reader);
            }

            /// <summary>
            /// Constructor for use in testing.
            /// </summary>
            /// <remarks>
            /// Application code will not create <see cref="DataKind"/> instances; the SDK maintains
            /// its own instances for the storable data types that it uses.
            /// </remarks>
            /// <param name="name">value for the <c>Name</c> property</param>
            /// <param name="serializer">function to convert an item to a serialized string form</param>
            /// <param name="deserializer">function to convert an item from a serialized string form</param>
            public DataKind(
                string name,
                Func<ItemDescriptor, string> serializer,
                Func<string, ItemDescriptor> deserializer)
            {
                _name = name;
                _serializer = serializer;
                _deserializer = deserializer;
                _internalSerializer = null;
                _internalDeserializer = null;
            }

            internal DataKind(
                string name,
                SerializerToJWriter internalSerializer,
                DeserializerFromJReader internalDeserializer)
            {
                _name = name;
                _internalSerializer = internalSerializer;
                _internalDeserializer = internalDeserializer;
                _serializer = item =>
                {
                    var w = JWriter.New();
                    if (item.Item is null)
                    {
                        var obj = w.Object();
                        obj.Name("version").Int(item.Version);
                        obj.Name("deleted").Bool(true);
                        obj.End();
                    }
                    else
                    {
                        internalSerializer(item.Item, w);
                    }
                    return w.GetString();
                };
                _deserializer = s =>
                {
                    var r = JReader.FromString(s);
                    return _internalDeserializer(ref r);
                };
            }

            /// <inheritdoc/>
            public override string ToString()
            {
                return "DataKind(" + _name + ")";
            }
        }
        
        /// <summary>
        /// A versioned item (or placeholder) storeable in an <see cref="IDataStore"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is used for data stores that directly store objects as-is, as the default
        /// in-memory store does. Items are typed as <see cref="object"/>; the store should
        /// not know or care what the actual object is.
        /// </para>
        /// <para>
        /// For any given key within a <see cref="DataKind"/>, there can be either an existing
        /// item with a version, or a "tombstone" placeholder representing a deleted item (also
        /// with a version. Deleted item placeholders are used so that if an item is first
        /// updated with version N and then deleted with version N+1, but the SDK receives those
        /// changes out of order, version N will not overwrite the deletion.
        /// </para>
        /// <para>
        /// Persistent data stores use <see cref="SerializedItemDescriptor"/> instead.
        /// </para>
        /// </remarks>
        public struct ItemDescriptor
        {
            private readonly int _version;
            private readonly object _item;

            /// <summary>
            /// The version number of this data, provided by the SDK.
            /// </summary>
            public int Version => _version;

            /// <summary>
            /// The data item, or null if this is a placeholder for a deleted item.
            /// </summary>
            public object Item => _item;

            /// <summary>
            /// Constructs an instance.
            /// </summary>
            /// <param name="version">the version number</param>
            /// <param name="item">the data item, or null for a deleted item</param>
            public ItemDescriptor(int version, object item)
            {
                _version = version;
                _item = item;
            }

            /// <summary>
            /// Shortcut for constructing a deleted item placeholder.
            /// </summary>
            /// <param name="version">the version number</param>
            /// <returns>the item descriptor</returns>
            public static ItemDescriptor Deleted(int version) => new ItemDescriptor(version, null);

            /// <inheritdoc/>
            public override string ToString() => "ItemDescriptor(" + Version + "," + Item;
        }

        /// <summary>
        /// A versioned item (or placeholder) storeable in an <see cref="IPersistentDataStore"/>
        /// or <see cref="IPersistentDataStoreAsync"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is equivalent to <see cref="ItemDescriptor"/>, but is used for persistent data
        /// stores. The SDK will convert each data item to and from its serialized string form;
        /// the persistent data store deals only with the serialized form.
        /// </para>
        /// </remarks>
        public struct SerializedItemDescriptor
        {
            /// <summary>
            /// The version number of this data, provided by the SDK.
            /// </summary>
            public int Version { get; }

            /// <summary>
            /// True if this is a placeholder (tombstone) for a deleted item.
            /// </summary>
            /// <remarks>
            /// If this is true, <see cref="SerializedItem"/> may still contain a string representing the
            /// deleted item, but the persistent store implementation has the option of not storing it if
            /// it can represent the placeholder in a more efficient way.
            /// </remarks>
            public bool Deleted { get; }

            /// <summary>
            /// The data item's serialized representatation.
            /// </summary>
            /// <remarks>
            /// This will never be null; for a deleted item placeholder, it will contain a special value
            /// that can be stored if necessary (see <see cref="Deleted"/>).
            /// </remarks>
            public string SerializedItem { get; }

            /// <summary>
            /// Constructs an instance.
            /// </summary>
            /// <param name="version">the version number</param>
            /// <param name="deleted">true if this is a deleted item placeholder</param>
            /// <param name="serializedItem">the serialized data item (will not be null)</param>
            public SerializedItemDescriptor(int version, bool deleted, string serializedItem)
            {
                Version = version;
                Deleted = deleted;
                SerializedItem = serializedItem;
            }

            /// <inheritdoc/>
            public override string ToString() => "SerializedItemDescriptor(" + Version + ","
                + Deleted + "," + SerializedItem + ")";
        }

        /// <summary>
        /// Wrapper for a set of storable items being passed to a data store.
        /// </summary>
        /// <remarks>
        /// Since the generic type signature for the data set is somewhat complicated (it is an
        /// ordered list of key-value pairs where each key is a <see cref="DataKind"/>, and
        /// each value is another ordered list of key-value pairs for the individual data items),
        /// this type simplifies the declaration of data store methods and makes it easier to
        /// see what the type represents.
        /// </remarks>
        /// <typeparam name="TDescriptor">will be <see cref="ItemDescriptor"/> or
        /// <see cref="SerializedItemDescriptor"/></typeparam>
        public struct FullDataSet<TDescriptor>
        {
            private readonly IEnumerable<KeyValuePair<DataKind, KeyedItems<TDescriptor>>> _data;

            /// <summary>
            /// The wrapped data set; may be empty, but will not be null.
            /// </summary>
            public IEnumerable<KeyValuePair<DataKind, KeyedItems<TDescriptor>>> Data => _data;

            /// <summary>
            /// Constructs an instance of this wrapper type.
            /// </summary>
            /// <param name="data">the data set</param>
            public FullDataSet(IEnumerable<KeyValuePair<DataKind, KeyedItems<TDescriptor>>> data)
            {
                _data = data ??
                    Enumerable.Empty<KeyValuePair<DataKind, KeyedItems<TDescriptor>>>();
            }

            /// <summary>
            /// Shortcut for constructing an empty data set.
            /// </summary>
            /// <returns>an instance containing no data</returns>
            public static FullDataSet<TDescriptor> Empty() => new FullDataSet<TDescriptor>(null);                
        }

        /// <summary>
        /// Wrapper for a set of storable items being passed to a data store, within a
        /// single <see cref="DataKind"/>.
        /// </summary>
        /// <remarks>
        /// This type exists only to provide a simpler type signature for data store methods, and to
        /// make it easier to see what the type represents. In particular, unlike an
        /// <see cref="IDictionary{TKey, TValue}"/>, the ordering of items may be significant
        /// (in the case of updates).
        /// </remarks>
        public struct KeyedItems<TDescriptor>
        {
            private readonly IEnumerable<KeyValuePair<string, TDescriptor>> _items;

            /// <summary>
            /// The wrapped data set; may be empty, but will not be null.
            /// </summary>
            public IEnumerable<KeyValuePair<string, TDescriptor>> Items => _items;

            /// <summary>
            /// Constructs an instance of this wrapper type.
            /// </summary>
            /// <param name="items">the data set</param>
            public KeyedItems(IEnumerable<KeyValuePair<string, TDescriptor>> items)
            {
                _items = items ??
                    Enumerable.Empty<KeyValuePair<string, TDescriptor>>();
            }

            /// <summary>
            /// Shortcut for constructing an empty data set.
            /// </summary>
            /// <returns>an instance containing no data</returns>
            public static KeyedItems<TDescriptor> Empty() => new KeyedItems<TDescriptor>(null);
        }
    }
}
