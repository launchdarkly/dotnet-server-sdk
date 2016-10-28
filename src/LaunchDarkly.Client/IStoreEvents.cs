namespace LaunchDarkly.Client
{
    public interface IStoreEvents
    {
        void Add(Event eventToLog);
        void Flush();
    }
}