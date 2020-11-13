using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    internal class DefaultEventProcessorWrapper : IEventProcessor
    {
        private readonly LaunchDarkly.Sdk.Interfaces.IEventProcessor _impl;

        internal DefaultEventProcessorWrapper(LaunchDarkly.Sdk.Interfaces.IEventProcessor impl)
        {
            _impl = impl;
        }

        public void SendEvent(LaunchDarkly.Sdk.Interfaces.Event e)
        {
            _impl.SendEvent(e);
        }

        public void Flush()
        {
            _impl.Flush();
        }

        public void Dispose()
        {
            _impl.Dispose();
        }
    }
}
