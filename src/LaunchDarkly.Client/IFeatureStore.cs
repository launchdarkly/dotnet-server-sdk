using System.Collections.Generic;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    interface IFeatureStore
    {
        Task<bool> WaitForInitializationAsync();
        FeatureFlag Get(string key);
        IDictionary<string, FeatureFlag> All();
        void Init(IDictionary<string, FeatureFlag> features);
        bool Initialized();
    }
}