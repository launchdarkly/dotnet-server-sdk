using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using static LaunchDarkly.Sdk.Internal.JsonConverterHelpers;
using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    // Deserialization of stream message data is all encapsulated here, so StreamProcessor can
    // deal with just the logical behavior of the stream and we can test this logic separately.
    //
    // All of the parsing methods have the following behavior:
    //
    // - They take the input data as a UTF-8 byte array rather than a string. This is because
    // System.Text.Json is designed to operate efficiently on UTF-8 data, and because StreamProcessor
    // sets the PreferDataAsUtf8Bytes option when creating the EventSource, the message data will
    // be read as raw bytes and passed to us directly-- without the inefficient step of convering it
    // to a UTF-16 string.
    //
    // - A JsonException is thrown for any malformed data. That includes 1. totally invalid JSON,
    // 2. well-formed JSON that is missing a necessary property for this message type.
    //
    // - For messages that have a "path" property, which might be for instance "/flags/xyz" to refer
    // to a feature flag with the key "xyz", an unrecognized path like "/cats/Lucy" is not considered
    // an error since it might mean LaunchDarkly now supports some new kind of data the SDK can't yet
    // use and should ignore. In this case we simply return null in place of a DataKind.

    internal static class StreamProcessorEvents
    {
        private static readonly string[] _putRequiredProperties = new string[] { "data" };
        private static readonly string[] _patchRequiredProperties = new string[] { "path", "data" };
        private static readonly string[] _deleteRequiredProperties = new string[] { "path", "version" };


        // This is the logical representation of the data in the "put" event. In the JSON representation,
        // the "data" property is actually a map of maps, but the schema we use internally is a list of
        // lists instead.
        //
        // The "path" property is normally always "/"; the LD streaming service sends this property, but
        // some versions of Relay do not, so we do not require it.
        //
        // Example JSON representation:
        //
        // {
        //   "path": "/",
        //   "data": {
        //     "flags": {
        //       "flag1": { "key": "flag1", "version": 1, ...etc. },
        //       "flag2": { "key": "flag2", "version": 1, ...etc. },
        //     },
        //     "segments": {
        //       "segment1": { "key", "segment1", "version": 1, ...etc. }
        //     }
        //   }
        // }
        internal class PutData
        {
            internal string Path { get; }
            internal FullDataSet<ItemDescriptor> Data { get; }

            internal PutData(string path, FullDataSet<ItemDescriptor> data)
            {
                Path = path;
                Data = data;
            }
        }

        // This is the logical representation of the data in the "patch" event. In the JSON representation,
        // there is a "path" property in the format "/flags/key" or "/segments/key", which we convert into
        // Kind and Key when we parse it. The "data" property is the JSON representation of the flag or
        // segment, which we deserialize into an ItemDescriptor.
        //
        // Example JSON representation:
        //
        // {
        //   "path": "/flags/flagkey",
        //   "data": {
        //     "key": "flagkey",
        //     "version": 2, ...etc.
        //   }
        // }
        internal class PatchData
        {
            internal DataKind Kind { get; }
            internal string Key { get; }
            internal ItemDescriptor Item { get; }

            internal PatchData(DataKind kind, string key, ItemDescriptor item)
            {
                Kind = kind;
                Key = key;
                Item = item;
            }
        }

        // This is the logical representation of the data in the "delete" event. In the JSON representation,
        // there is a "path" property in the format "/flags/key" or "/segments/key", which we convert into
        // Kind and Key when we parse it.
        //
        // Example JSON representation:
        //
        // {
        //   "path": "/flags/flagkey",
        //   "version": 3
        // }
        internal class DeleteData
        {
            internal DataKind Kind { get; }
            internal string Key { get; }
            internal int Version { get; }

            internal DeleteData(DataKind kind, string key, int version)
            {
                Kind = kind;
                Key = key;
                Version = version;
            }
        }

        internal static PutData ParsePutData(byte[] json)
        {
            var r = new Utf8JsonReader(json);
            string path = null;
            FullDataSet<ItemDescriptor> data = new FullDataSet<ItemDescriptor>();

            for (var obj = RequireObject(ref r).WithRequiredProperties(_putRequiredProperties); obj.Next(ref r);)
            {
                switch (obj.Name)
                {
                    case "path":
                        path = r.GetString();
                        break;
                    case "data":
                        data = ParseFullDataset(ref r);
                        break;
                }
            }
            return new PutData(path, data);
        }

        internal static FullDataSet<ItemDescriptor> ParseFullDataset(ref Utf8JsonReader r)
        {
            var dataBuilder = ImmutableList.CreateBuilder<KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>>();
            for (var topLevelObj = RequireObject(ref r); topLevelObj.Next(ref r);)
            {
                var name = topLevelObj.Name;
                var kind = DataModel.AllDataKinds.FirstOrDefault(k => name == PathNameForKind(k));
                if (kind == null)
                {
                    continue;
                }
                var itemsBuilder = ImmutableList.CreateBuilder<KeyValuePair<string, ItemDescriptor>>();
                for (var itemsObj = RequireObject(ref r); itemsObj.Next(ref r);)
                {
                    var key = itemsObj.Name;
                    var item = kind.DeserializeFromJsonReader(ref r);
                    itemsBuilder.Add(new KeyValuePair<string, ItemDescriptor>(key, item));
                }
                dataBuilder.Add(new KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>(kind,
                    new KeyedItems<ItemDescriptor>(itemsBuilder.ToImmutable())));
            }
            return new FullDataSet<ItemDescriptor>(dataBuilder.ToImmutable());
        }

        internal static PatchData ParsePatchData(byte[] json)
        {
            var r = new Utf8JsonReader(json);
            DataKind kind = null;
            string key = null;
            for (var obj = RequireObject(ref r).WithRequiredProperties(_patchRequiredProperties); obj.Next(ref r);)
            {
                switch (obj.Name)
                {
                    case "path":
                        TryParsePath(r.GetString(), out kind, out key);
                        if (kind is null)
                        {
                            // An unrecognized path isn't considered an error; we'll just return a null kind,
                            // indicating that we should ignore this event.
                            return new PatchData(null, null, new ItemDescriptor());
                        }
                        break;
                    case "data":
                        if (kind != null)
                        {
                            // If kind is null here, it means we happened to read the "data" property before
                            // the "path" property, so we don't yet know what kind of data model object this
                            // is, so we can't parse it yet and we'll have to do a second pass.
                            var item = kind.DeserializeFromJsonReader(ref r);
                            return new PatchData(kind, key, item);
                        }
                        break;
                }
            }
            // If we got here, it means we couldn't parse the data model object yet because we saw the
            // "data" property first. But we definitely saw both properties (otherwise we would've got
            // an error due to using WithRequiredProperties) so kind is now non-null.
            var r1 = new Utf8JsonReader(json);
            for (var obj = RequireObject(ref r1); obj.Next(ref r1);)
            {
                if (obj.Name == "data")
                {
                    return new PatchData(kind, key, kind.DeserializeFromJsonReader(ref r1));
                }
            }
            // Shouldn't be able to get this far, because the first pass should have failed if there
            // was no "data" property.
            throw new JsonException("unexpected error in ParsePatchData");
        }

        internal static DeleteData ParseDeleteData(byte[] json)
        {
            var r = new Utf8JsonReader(json);
            DataKind kind = null;
            string key = null;
            int version = 0;
            for (var obj = RequireObject(ref r).WithRequiredProperties(_deleteRequiredProperties); obj.Next(ref r);)
            {
                switch (obj.Name)
                {
                    case "path":
                        TryParsePath(r.GetString(), out kind, out key);
                        break;
                    case "version":
                        version = r.GetInt32();
                        break;
                }
            }
            return new DeleteData(kind, key, version);
        }

        internal static string PathNameForKind(DataKind kind) =>
            (kind == DataModel.Features) ? "flags" : kind.Name;

        internal static bool TryParsePath(string path, out DataKind kindOut, out string keyOut)
        {
            foreach (var kind in DataModel.AllDataKinds)
            {
                var prefix = "/" + PathNameForKind(kind) + "/";
                if (path.StartsWith(prefix))
                {
                    kindOut = kind;
                    keyOut = path.Substring(prefix.Length);
                    return true;
                }
            }
            kindOut = null;
            keyOut = null;
            return false;
        }
    }
}
