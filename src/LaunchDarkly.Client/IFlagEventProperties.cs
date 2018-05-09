using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.Client
{
    internal interface IFlagEventProperties
    {
        string Key { get; }
        int Version { get; }
        bool TrackEvents { get; }
        long? DebugEventsUntilDate { get; }
    }
}
