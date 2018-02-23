namespace LaunchDarkly.Client
{
    /// <summary>
    /// Interface for an object that sends or stores analytics events. The default implementation
    /// sends all analytics events to LaunchDarkly.
    /// </summary>
    public interface IStoreEvents
    {
        /// <summary>
        /// Processes an event.
        /// </summary>
        /// <param name="eventToLog">the event</param>
        void Add(Event eventToLog);

        /// <summary>
        /// If events are being buffered, flushes the buffer.
        /// </summary>
        void Flush();
    }
}