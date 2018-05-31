
namespace LaunchDarkly.Common.Tests
{
    // A minimal implementation of IFlagEventProperties for use in the common unit tests.
    internal class FlagEventPropertiesImpl : IFlagEventProperties
    {
        public string Key { get; internal set; }
        public int Version { get; internal set; }
        public bool TrackEvents { get; internal set; }
        public long? DebugEventsUntilDate { get; internal set; }
    }

    internal class FlagEventPropertiesBuilder
    {
        private readonly string _key;
        private int _version;
        private bool _trackEvents;
        private long? _debugEventsUntilDate;

        internal FlagEventPropertiesBuilder(string key)
        {
            _key = key;
        }

        internal FlagEventPropertiesBuilder(IFlagEventProperties from)
        {
            _key = from.Key;
            _version = from.Version;
            _trackEvents = from.TrackEvents;
            _debugEventsUntilDate = from.DebugEventsUntilDate;
        }

        internal IFlagEventProperties Build()
        {
            return new FlagEventPropertiesImpl
            {
                Key = _key,
                Version = _version,
                TrackEvents = _trackEvents,
                DebugEventsUntilDate = _debugEventsUntilDate
            };
        }

        internal FlagEventPropertiesBuilder Version(int version)
        {
            _version = version;
            return this;
        }
        
        internal FlagEventPropertiesBuilder TrackEvents(bool trackEvents)
        {
            _trackEvents = trackEvents;
            return this;
        }

        internal FlagEventPropertiesBuilder DebugEventsUntilDate(long? debugEventsUntilDate)
        {
            _debugEventsUntilDate = debugEventsUntilDate;
            return this;
        }
    }
}
