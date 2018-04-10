using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Stub implementation of <see cref="IEventProcessor"/> for when we don't want to send any events.
    /// </summary>
    internal sealed class NullEventProcessor : IEventProcessor
    {
        public void SendEvent(Event evt)
        {
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }
    }
}
