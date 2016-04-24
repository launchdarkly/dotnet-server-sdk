using System;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    interface IUpdateProcessor : IDisposable
    {
        TaskCompletionSource<bool> Start();
        bool Initialized();
    }
}
