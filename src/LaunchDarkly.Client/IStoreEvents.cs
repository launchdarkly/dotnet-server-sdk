using System;

namespace LaunchDarkly.Client
{
    public interface IStoreEvents : IDisposable
    {
        void Add(Event eventToLog);
        void Flush();
    }
}