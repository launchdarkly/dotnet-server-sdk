using System;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    internal interface IUpdateProcessor : IDisposable
    {
        TaskCompletionSource<bool> Start();
        bool Initialized();
    }
}