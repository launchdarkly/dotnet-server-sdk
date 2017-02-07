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
        void Delete(string key, int version);
        void Upsert(string key, FeatureFlag featureFlag);
        bool Initialized();
    }
}