using System;
namespace LaunchDarkly.Client
{
    /// <summary>
    /// Common interface for string-keyed, versioned objects that can be kept in an IFeatureStore.
    /// </summary>
    public interface IVersionedData
    {
        string Key { get; }
        int Version { get; set; }
        bool Deleted { get; set; }
    }
}
