using System;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal static class DataKinds
    {
        internal static DataKind MakeDataKind(string name, Type expectedType)
        {
            return new DataKind(name,
                o =>
                {
                    if (o.GetType() == expectedType)
                    {
                        return JsonUtil.EncodeJson(o);
                    }
                    throw new ArgumentException("tried to serialize " + o.GetType() + " as " + expectedType);
                },
                s =>
                {
                    return JsonUtil.DecodeJson(s, expectedType);
                });
        }

        internal static readonly DataKind Features = MakeDataKind("features", typeof(FeatureFlag));

        internal static readonly DataKind Segments = MakeDataKind("segments", typeof(Segment));
    }
}
