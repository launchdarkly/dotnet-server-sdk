using System;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for an object that can send or store analytics events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Application code normally does not need to interact with <see cref="IEventProcessor"/> or its
    /// related parameter types. They are provided to allow a custom implementation or test fixture to be
    /// substituted for the SDK's normal analytics event logic.
    /// </para>
    /// <para>
    /// All of the <c>Record</c> methods must return as soon as possible without waiting for events to be
    /// delivered; event delivery is done asynchronously by a background task.
    /// </para>
    /// </remarks>
    public interface IEventProcessor : IDisposable
    {
        /// <summary>
        /// Records the action of evaluating a feature flag.
        /// </summary>
        /// <remarks>
        /// Depending on the feature flag properties and event properties, this may be transmitted to the
        /// events service as an individual event, or may only be added into summary data.
        /// </remarks>
        /// <param name="e">parameters for an evaluation event</param>
        void RecordEvaluationEvent(EventProcessorTypes.EvaluationEvent e);

        /// <summary>
        /// Records a set of user properties.
        /// </summary>
        /// <param name="e">parameters for an identify event</param>
        void RecordIdentifyEvent(EventProcessorTypes.IdentifyEvent e);

        /// <summary>
        /// Records a custom event.
        /// </summary>
        /// <param name="e">parameters for a custom event</param>
        void RecordCustomEvent(EventProcessorTypes.CustomEvent e);

        /// <summary>
        /// Records an alias event.
        /// </summary>
        void RecordAliasEvent(EventProcessorTypes.AliasEvent e);

        /// <summary>
        /// Specifies that any buffered events should be sent as soon as possible, rather than waiting
        /// for the next flush interval.
        /// </summary>
        /// <remarks>
        /// This method triggers an asynchronous task, so events still may not be sent until a later
        /// until a later time. However, calling <see cref="IDisposable.Dispose"/> will synchronously
        /// deliver any events that were not yet delivered prior to shutting down.
        /// </remarks>
        void Flush();
    }
}
