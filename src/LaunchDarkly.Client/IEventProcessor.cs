using System;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Interface for an object that can send or store analytics events.
    /// </summary>
    public interface IEventProcessor : IDisposable
    {
        /// <summary>
        /// Processes an event. This method is asynchronous; the event may be sent later in the background
        /// at an interval set by <see cref="Configuration.EventQueueFrequency"/>, or due to a call to
        /// <see cref="Flush"/>.
        /// </summary>
        /// <param name="evt">the event</param>
        void SendEvent(Event evt);

        /// <summary>
        /// Finishes processing any events that have been buffered. In the default implementation, this means
        /// sending the events to LaunchDarkly.This method is synchronous; when it returns, you can assume
        /// that all events queued prior to the <c>Flush</c> have now been delivered.
        /// </summary>
        void Flush();
    }
}