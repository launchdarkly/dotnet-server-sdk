using System;
namespace LaunchDarkly.Client
{
    public interface IVersionedData
    {
        string Key { get; }
        int Version { get; set; }
        bool Deleted { get; set; }
    }
}
