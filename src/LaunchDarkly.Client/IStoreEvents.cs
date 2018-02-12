namespace LaunchDarkly.Client
{
    internal interface IStoreEvents
    {
        void Add(Event eventToLog);
        void Flush();
    }
}