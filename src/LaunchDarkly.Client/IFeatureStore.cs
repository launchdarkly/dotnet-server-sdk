using System.Collections.Generic;

namespace LaunchDarkly.Client
{
    public interface IFeatureStore
    {
        Feature Get(string key);
        IDictionary<string, Feature> All();
        void Init(IDictionary<string, Feature> features);
        void Delete(string key, int version);
        void Upsert(string key, Feature feature);
        bool Initialized();
    }
}
