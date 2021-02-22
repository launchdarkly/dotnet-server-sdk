using System;
using System.Threading.Tasks;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// An interface for querying the status of the SDK's data source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The data source is the component that receives updates to feature flag data. Normally this is a streaming
    /// connection, but it could be polling or file data depending on your configuration.
    /// </para>
    /// <para>
    /// An implementation of this interface is returned by <see cref="ILdClient.DataSourceStatusProvider"/>.
    /// Application code never needs to implement this interface.
    /// </para>
    /// </remarks>
    /// <seealso cref="ILdClient.DataSourceStatusProvider"/>
    public interface IDataSourceStatusProvider
    {
        /// <summary>
        /// The current status of the data source.
        /// </summary>
        /// <remarks>
        /// <para>
        /// All of the built-in data source implementations are guaranteed to update this status whenever they
        /// successfully initialize, encounter an error, or recover after an error.
        /// </para>
        /// <para>
        /// For a custom data source implementation, it is the responsibility of the data source to report its
        /// status via <see cref="IDataSourceUpdates"/>; if it does not do so, the status will always be reported
        /// as <see cref="DataSourceState.Initializing"/>.
        /// </para>
        /// </remarks>
        DataSourceStatus Status { get; }

        /// <summary>
        /// An event for receiving notifications of status changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any handlers attached to this event will be notified whenever any property of the status has changed.
        /// See <see cref="DataSourceStatus"/>for an explanation of the meaning of each property and what could cause it
        /// to change.
        /// </para>
        /// <para>
        /// Notifications will be dispatched on a background task. It is the listener's responsibility to return
        /// as soon as possible so as not to block subsequent notifications.
        /// </para>
        /// </remarks>
        event EventHandler<DataSourceStatus> StatusChanged;

        /// <summary>
        /// A synchronous method for waiting for a desired connection state.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the current state is already <paramref name="desiredState"/> when this method is called, it immediately
        /// returns. Otherwise, it blocks until 1. the state has become <paramref name="desiredState"/>, 2. the state
        /// has become <see cref="DataSourceState.Off"/> (since that is a permanent condition), or 3. the specified
        /// timeout elapses.
        /// </para>
        /// <para>
        /// A scenario in which this might be useful is if you want to create the <see cref="LdClient"/> without waiting
        /// for it to initialize, and then wait for initialization at a later time or on a different thread:
        /// </para>
        /// <code>
        ///     // create the client but do not wait
        ///     var config = Configuration.Builder("my-sdk-key").StartWaitTime(TimeSpan.Zero).Build();
        ///     var client = new LDClient(config);
        ///
        ///     // later, possibly on another thread:
        ///     var inited = client.DataSourceStatusProvider.WaitFor(DataSourceState.Valid,
        ///         TimeSpan.FromSeconds(10));
        ///     if (!inited) {
        ///         // do whatever is appropriate if initialization has timed out
        ///     }       
        /// </code>
        /// </remarks>
        /// <param name="desiredState">the desired connection state (normally this would be
        /// <see cref="DataSourceState.Valid"/>)</param>
        /// <param name="timeout">the maximum amount of time to wait-- or <see cref="TimeSpan.Zero"/> to block
        /// indefinitely</param>
        /// <returns>true if the connection is now in the desired state; false if it timed out, or if the state
        /// changed to <see cref="DataSourceState.Off"/> and that was not the desired state</returns>
        /// <seealso cref="WaitForAsync(DataSourceState, TimeSpan)"/>
        bool WaitFor(DataSourceState desiredState, TimeSpan timeout);

        /// <summary>
        /// An asynchronous method for waiting for a desired connection state.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method behaves identically to <see cref="WaitFor"/> except that it is asynchronous. The following
        /// example is the asynchronous equivalent of the example code shown for <see cref="WaitFor"/>:
        /// </para>
        /// <code>
        ///     // create the client but do not wait
        ///     var config = Configuration.Builder("my-sdk-key").StartWaitTime(TimeSpan.Zero).Build();
        ///     var client = new LDClient(config);
        ///
        ///     // later, possibly on another thread:
        ///     var inited = await client.DataSourceStatusProvider.WaitFor(DataSourceState.Valid,
        ///         TimeSpan.FromSeconds(10));
        ///     if (!inited) {
        ///         // do whatever is appropriate if initialization has timed out
        ///     }       
        /// </code>
        /// </remarks>
        /// <param name="desiredState">the desired connection state (normally this would be
        /// <see cref="DataSourceState.Valid"/>)</param>
        /// <param name="timeout">the maximum amount of time to wait-- or <see cref="TimeSpan.Zero"/> to block
        /// indefinitely</param>
        /// <returns>true if the connection is now in the desired state; false if it timed out, or if the state
        /// changed to <see cref="DataSourceState.Off"/> and that was not the desired state</returns>
        /// <seealso cref="WaitFor(DataSourceState, TimeSpan)"/>
        Task<bool> WaitForAsync(DataSourceState desiredState, TimeSpan timeout);
    }
}
