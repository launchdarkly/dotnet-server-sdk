namespace LaunchDarkly.Client
{
    /// <summary>
    /// Interface for an object that sends or stores analytics events. The default implementation
    /// sends all analytics events to LaunchDarkly.
    /// </summary>
    /// <seealso cref="IEventProcessorFactory"/>
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

    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IStoreEvents"/>.
    /// </summary>
    /// <seealso cref="ConfigurationExtensions.WithEventProcessorFactory(Configuration, IEventProcessorFactory)"/>
    /// <seealso cref="Implementations"/>
    public interface IEventProcessorFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <param name="config">the LaunchDarkly configuration</param>
        /// <returns>an <c>IStoreEvents</c> instance</returns>
        IStoreEvents CreateEventProcessor(Configuration config);
    }

    /// <see cref="Implementations.NullEventProcessor"/>
    internal class NullEventProcessor : IStoreEvents
    {
        void IStoreEvents.Add(Event eventToLog)
        { }

        void IStoreEvents.Flush()
        { }
    }
}