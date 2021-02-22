
namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal static class FlagFactory
    {
        // Constructs a flag that always returns the same value. This is done by giving it a
        // single variation and setting the fallthrough variation to that.
        public static object FlagWithValue(string key, LdValue value)
        {
            var json = LdValue.BuildObject()
                .Add("key", key)
                .Add("version", 1)
                .Add("on", true)
                .Add("variations", LdValue.ArrayOf(value))
                .Add("fallthrough", LdValue.BuildObject().Add("variation", 0).Build())
                .Build()
                .ToJsonString();
            return DataModel.Features.Deserialize(json).Item;
        }
    }
}
