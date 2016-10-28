using System.Collections.Generic;

namespace LaunchDarkly.Client
{
    interface IFeatureStore
    {
        FeatureFlag Get(string key);
        IDictionary<string, FeatureFlag> All();
        void Init(IDictionary<string, FeatureFlag> features);
        void Delete(string key, int version);
        void Upsert(string key, FeatureFlag featureFlag);
        bool Initialized();
    }
}