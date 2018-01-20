using System.Collections.Generic;

namespace LaunchDarkly.Client
{
    public interface ISegmentStore
    {
        Segment Get(string key);
        IDictionary<string, Segment> All();
        void Init(IDictionary<string, Segment> segments);
        void Delete(string key, int version);
        void Upsert(string key, Segment segment);
        bool Initialized();
    }
}