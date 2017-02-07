using System.Collections.Generic;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    interface IFeatureStore
    {
        string VersionIdentifier { get; }
        Task<bool> WaitForInitializationAsync();
        FeatureFlag Get(string key);
        IDictionary<string, FeatureFlag> All();
        void Init(IDictionary<string, FeatureFlag> features, string versionIdentifier);
        bool Initialized();
    }
}