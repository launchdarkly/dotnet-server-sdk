
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Common interface for string-keyed, versioned objects that can be kept in the SDK's data store.
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
