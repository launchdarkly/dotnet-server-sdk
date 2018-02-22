
namespace LaunchDarkly.Client
{
    public static class LdStoreEventsFactory
    {
        public static IStoreEvents Create(Configuration config)
        {
            return new EventProcessor(config);
        }
    }
}
