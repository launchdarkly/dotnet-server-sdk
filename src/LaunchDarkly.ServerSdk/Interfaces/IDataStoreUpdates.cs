
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface that an implementation of <see cref="IDataStore"/> can use to report
    /// information back to the SDK.
    /// </summary>
    /// <remarks>
    /// The <see cref="IDataStoreFactory"/> receives an implementation of this interface
    /// and can pass it to the data store that it creates, if desired.
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
