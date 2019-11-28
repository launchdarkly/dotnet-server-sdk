
namespace LaunchDarkly.Sdk.Server.Model
{
    internal interface IVersionedData
    {
        string Key { get; }
        int Version { get; }
    }
}
