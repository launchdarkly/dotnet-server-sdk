using System;
using System.Collections.Generic;
using System.Linq;

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
        public class DataKind
        {
            private readonly string _name;
            private readonly Func<object, string> _serializer;
            private readonly Func<string, object> _deserializer;

            /// <summary>
            /// An alphabetic string that uniquely identifies this data kind.
            /// </summary>
            /// <remarks>
            /// This is in effect a namespace for a collection of items of the same kind. Item
            /// keys must be unique within that namespace. Persistent data store implementations
            /// could use this string as part of a composite key or table name.
            /// </remarks>
            public string Name => _name;

            internal string Serialize(object o) => _serializer(o);

            internal object Deserialize(string serializedData) => _deserializer(serializedData);

            /// <summary>
            /// Constructor for use in testing.
            /// </summary>
            /// <remarks>
            /// Application code will not create <see cref="DataKind"/> instances; the SDK maintains
            /// its own instances for the storable data types that it uses.
            /// </remarks>
            /// <param name="name">value for the <c>Name</c> property</param>
            public DataKind(string name) : this(name, null, null) { }

            internal DataKind(string name, Func<object, string> serializer,
                Func<string, object> deserializer)
            {
                _name = name;
                _serializer = serializer;
                _deserializer = deserializer;
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
            private readonly int _version;
            private readonly string _serializedItem;

            /// <summary>
            /// The version number of this data, provided by the SDK.
            /// </summary>
            public int Version => _version;

            /// <summary>
            /// The serialized data item, or null if this is a placeholder for a deleted item.
            /// </summary>
            public string SerializedItem => _serializedItem;

            /// <summary>
            /// Constructs an instance.
            /// </summary>
            /// <param name="version">the version number</param>
            /// <param name="serializedItem">the serialized data item, or null for a deleted item</param>
            public SerializedItemDescriptor(int version, string serializedItem)
            {
                _version = version;
                _serializedItem = serializedItem;
            }

            /// <summary>
            /// Shortcut for constructing a deleted item placeholder.
            /// </summary>
            /// <param name="version">the version number</param>
            /// <returns>the item descriptor</returns>
            public static SerializedItemDescriptor Deleted(int version) =>
                new SerializedItemDescriptor(version, null);

            /// <inheritdoc/>
            public override string ToString() => "SerializedItemDescriptor(" + Version + "," + SerializedItem;
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
            private readonly IEnumerable<KeyValuePair<DataKind, IEnumerable<KeyValuePair<string, TDescriptor>>>> _data;

            /// <summary>
            /// The wrapped data set.
            /// </summary>
            public IEnumerable<KeyValuePair<DataKind, IEnumerable<KeyValuePair<string, TDescriptor>>>> Data => _data;

            /// <summary>
            /// Constructs an instance of this wrapper type.
            /// </summary>
            /// <param name="data">the data set</param>
            public FullDataSet(IEnumerable<KeyValuePair<DataKind, IEnumerable<KeyValuePair<string, TDescriptor>>>> data)
            {
                _data = data;
            }

            /// <summary>
            /// Shortcut for constructing an empty data set.
            /// </summary>
            /// <returns>an instance containing no data</returns>
            public static FullDataSet<TDescriptor> Empty() => new FullDataSet<TDescriptor>(
                Enumerable.Empty<KeyValuePair<DataKind, IEnumerable<KeyValuePair<string, TDescriptor>>>>());
        }
    }
}
