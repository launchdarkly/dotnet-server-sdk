using System;
namespace LaunchDarkly.Client
{
    /// <summary>
    /// Common interface for string-keyed, versioned objects that can be kept in an <see cref="IFeatureStore"/>.
    /// </summary>
    public interface IVersionedData
    {
        /// <summary>
        /// The unique string key of this object.
        /// </summary>
        string Key { get; }
        /// <summary>
        /// The version number of this object.
        /// </summary>
        int Version { get; set; }
        /// <summary>
        /// True if this object has been deleted.
        /// </summary>
        bool Deleted { get; set; }
    }
}
