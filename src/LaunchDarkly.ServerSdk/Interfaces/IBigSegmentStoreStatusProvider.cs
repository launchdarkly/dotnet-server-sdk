using System;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// An interface for querying the status of a big segment store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The big segment store is the component that receives information about big segments, normally
    /// from a database populated by the LaunchDarkly Relay Proxy. "Big segments" are a specific type
    /// of user segments. For more information, read the LaunchDarkly documentation about user
    /// segments: https://docs.launchdarkly.com/home/users
    /// </para>
    /// <para>
    /// An implementation of this interface is returned by <see cref="LdClient.BigSegmentStoreStatusProvider"/>.
    /// Application code never needs to implement this interface.
    /// </para>
    /// </remarks>
    public interface IBigSegmentStoreStatusProvider
    {
        /// <summary>
        /// The current status of the store.
        /// </summary>
        BigSegmentStoreStatus Status { get; }

        /// <summary>
        /// An event for receiving notifications of status changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any handlers attached to this event will be notified whenever any property of the status has changed.
        /// See <see cref="BigSegmentStoreStatus"/>for an explanation of the meaning of each property and what
        /// could cause it to change.
        /// </para>
        /// <para>
        /// Notifications will be dispatched on a background task. It is the listener's responsibility to return
        /// as soon as possible so as not to block subsequent notifications.
        /// </para>
        /// </remarks>
        event EventHandler<BigSegmentStoreStatus> StatusChanged;
    }
}
