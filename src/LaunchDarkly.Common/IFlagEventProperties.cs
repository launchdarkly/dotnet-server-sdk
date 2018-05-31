namespace LaunchDarkly.Common
{
    /// <summary>
    /// Common interface for the subset of feature flag properties that are required for the event logic
    /// (since the details of the feature flag model are different between server-side and mobile).
    /// </summary>
    internal interface IFlagEventProperties
    {
        string Key { get; }
        int Version { get; }
        bool TrackEvents { get; }
        long? DebugEventsUntilDate { get; }
    }
}
