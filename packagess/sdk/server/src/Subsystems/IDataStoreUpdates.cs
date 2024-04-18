
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Subsystems
{
    /// <summary>
    /// Interface that an implementation of <see cref="IDataStore"/> can use to report
    /// information back to the SDK.
    /// </summary>
    /// <remarks>
    /// Component factories for <see cref="IDataStore"/> implementations will receive an implementation of this
    /// interface in the <see cref="LdClientContext.DataStoreUpdates"/> property of <see cref="LdClientContext"/>.
    /// </remarks>
    public interface IDataStoreUpdates
    {
        /// <summary>
        /// Reports a change in the data store's operational status.
        /// </summary>
        /// <remarks>
        /// This is what makes the status monitoring mechanisms in
        /// <see cref="IDataStoreStatusProvider"/> work.
        /// </remarks>
        /// <param name="newStatus">the updated status properties</param>
        void UpdateStatus(DataStoreStatus newStatus);
    }
}
