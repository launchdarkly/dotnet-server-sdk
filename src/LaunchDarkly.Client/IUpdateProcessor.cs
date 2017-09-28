using System;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    internal interface IUpdateProcessor : IDisposable
    {
        Task<bool> Start();
        bool Initialized();
    }
}