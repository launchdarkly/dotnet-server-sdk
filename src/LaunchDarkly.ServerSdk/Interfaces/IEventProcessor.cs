using System;
using LaunchDarkly.Sdk.Interfaces;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for an object that can send or store analytics events.
    /// </summary>
    public interface IEventProcessor : IDisposable
    {
        /// <summary>
        /// Records an event asynchronously.
        /// </summary>
        /// <param name="e">an event</param>
        void SendEvent(Event e);

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
