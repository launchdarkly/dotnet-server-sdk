using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.JsonStream;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
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

        internal static PutData ParsePutData(string json)
        {
            var r = JReader.FromString(json);
            try
            {
                string path = null;
                FullDataSet<ItemDescriptor> data = new FullDataSet<ItemDescriptor>();

                for (var obj = r.Object().WithRequiredProperties(_putRequiredProperties); obj.Next(ref r);)
                {
                    if (obj.Name == "path")
                    {
                        path = r.String();
                    }
                    else if (obj.Name == "data")
                    {
                        data = ParseFullDataset(ref r);
                    }
                }
                return new PutData(path, data);
            }
            catch (Exception e)
            {
                throw r.TranslateException(e);
            }
        }

        internal static FullDataSet<ItemDescriptor> ParseFullDataset(ref JReader r)
        {
            try
            {
                var dataBuilder = ImmutableList.CreateBuilder<KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>>();
                for (var topLevelObj = r.Object(); topLevelObj.Next(ref r);)
                {
                    var name = topLevelObj.Name.ToString();
                    var kind = DataModel.AllDataKinds.FirstOrDefault(k => name == PathNameForKind(k));
                    if (kind == null)
                    {
                        continue;
                    }
                    var itemsBuilder = ImmutableList.CreateBuilder<KeyValuePair<string, ItemDescriptor>>();
                    for (var itemsObj = r.Object(); itemsObj.Next(ref r);)
                    {
                        var key = itemsObj.Name.ToString();
                        var item = kind.DeserializeFromJReader(ref r);
                        itemsBuilder.Add(new KeyValuePair<string, ItemDescriptor>(key, item));
                    }
                    dataBuilder.Add(new KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>(kind,
                        new KeyedItems<ItemDescriptor>(itemsBuilder.ToImmutable())));
                }
                return new FullDataSet<ItemDescriptor>(dataBuilder.ToImmutable());
            }
            catch (Exception e)
            {
                throw r.TranslateException(e);
            }
        }

        internal static PatchData ParsePatchData(string json)
        {
            var r = JReader.FromString(json);
            try
            {
                DataKind kind = null;
                string key = null;
                for (var obj = r.Object().WithRequiredProperties(_patchRequiredProperties); obj.Next(ref r);)
                {
                    if (obj.Name == "path")
                    {
                        TryParsePath(r.String(), out kind, out key);
                        if (kind is null)
                        {
                            // An unrecognized path isn't considered an error; we'll just return a null kind,
                            // indicating that we should ignore this event.
                            return new PatchData(null, null, new ItemDescriptor());
                        }
                    }
                    else if (obj.Name == "data")
                    {
                        if (kind != null)
                        {
                            // If kind is null here, it means we happened to read the "data" property before
                            // the "path" property, so we don't yet know what kind of data model object this
                            // is, so we can't parse it yet and we'll have to do a second pass.
                            var item = kind.DeserializeFromJReader(ref r);
                            return new PatchData(kind, key, item);
                        }
                    }
                }
                // If we got here, it means we couldn't parse the data model object yet because we saw the
                // "data" property first. But we definitely saw both properties (otherwise we would've got
                // an error due to using WithRequiredProperties) so kind is now non-null.
                var r1 = JReader.FromString(json);
                for (var obj = r1.Object(); obj.Next(ref r1);)
                {
                    if (obj.Name == "data")
                    {
                        return new PatchData(kind, key, kind.DeserializeFromJReader(ref r1));
                    }
                }
                throw new RequiredPropertyException("data", json.Length);
            }
            catch (Exception e)
            {
                throw r.TranslateException(e);
            }
        }

        internal static DeleteData ParseDeleteData(string json)
        {
            var r = JReader.FromString(json);
            try
            {
                DataKind kind = null;
                string key = null;
                int version = 0;
                for (var obj = r.Object().WithRequiredProperties(_deleteRequiredProperties); obj.Next(ref r);)
                {
                    if (obj.Name == "path")
                    {
                        TryParsePath(r.String(), out kind, out key);
                    }
                    else if (obj.Name == "version")
                    {
                        version = r.Int();
                    }
                }
                return new DeleteData(kind, key, version);
            }
            catch (Exception e)
            {
                throw r.TranslateException(e);
            }
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
