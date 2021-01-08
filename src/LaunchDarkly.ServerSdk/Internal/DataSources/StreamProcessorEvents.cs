using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal static class StreamProcessorEvents
    {
        internal class PutData
        {
            internal AllData Data { get; private set; }

            [JsonConstructor]
            internal PutData(AllData data)
            {
                Data = data;
            }
        }

        internal class PatchData
        {
            internal string Path { get; private set; }
            internal JToken Data { get; private set; }

            [JsonConstructor]
            internal PatchData(string path, JToken data)
            {
                Path = path;
                Data = data;
            }
        }

        internal class DeleteData
        {
            internal string Path { get; private set; }
            internal int Version { get; private set; }

            [JsonConstructor]
            internal DeleteData(string path, int version)
            {
                Path = path;
                Version = version;
            }
        }

        internal static bool GetKeyFromPath(string path, DataKind kind, out string key)
        {
            if (path.StartsWith(GetDataKindPath(kind)))
            {
                key = path.Substring(GetDataKindPath(kind).Length);
                return true;
            }
            key = null;
            return false;
        }

        private static string GetDataKindPath(DataKind kind)
        {
            if (kind == DataModel.Features)
            {
                return "/flags/";
            }
            else if (kind == DataModel.Segments)
            {
                return "/segments/";
            }
            return null;
        }
    }
}
