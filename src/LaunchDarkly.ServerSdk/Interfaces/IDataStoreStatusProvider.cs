using System;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// An interface for querying the status of a persistent data store.
    /// </summary>
    /// <remarks>
    /// An implementation of this interface is returned by <see cref="ILdClient.DataStoreStatusProvider"/>.
    /// Application code should not implement this interface.
    /// </remarks>
    public interface IDataStoreStatusProvider
    {
        /// <summary>
        /// The current status of the store.
        /// </summary>
        /// <remarks>
        /// This is only meaningful for persistent stores, or any other <see cref="IDataStore"/>
        /// implementation that makes use of the reporting mechanism provided by <see cref="IDataStoreUpdates"/>.
        /// For the default in-memory store, the status will always be reported as "available".
        /// </remarks>
        DataStoreStatus Status { get; }

        /// <summary>
        /// Indicates whether the current data store implementation supports status monitoring.
        /// </summary>
        /// <remarks>
        /// This is normally true for all persistent data stores, and false for the default in-memory store.
        /// A true value means that any status event listeners can expect to be notified if there is any
        /// error in storing data, and then notified again when the error condition is resolved. A false
        /// value means that the status is not meaningful and listeners should not expect to be notified.
        /// </remarks>
        bool StatusMonitoringEnabled { get; }

        /// <summary>
        /// An event for receiving notifications of status changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any handlers attached to this event will be notified whenever any property of the status has changed.
        /// See <see cref="DataStoreStatus"/>for an explanation of the meaning of each property and what could cause it
        /// to change.
        /// </para>
        /// <para>
        /// Notifications will be dispatched on a background task. It is the listener's responsibility to return
        /// as soon as possible so as not to block subsequent notifications.
        /// </para>
        /// </remarks>
        event EventHandler<DataStoreStatus> StatusChanged;
    }
}
