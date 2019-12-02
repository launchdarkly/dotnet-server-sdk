
namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal interface IVersionedData
    {
        string Key { get; }
        int Version { get; }
    }
}
