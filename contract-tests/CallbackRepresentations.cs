using System.Collections.Generic;

namespace TestService
{
    public class BigSegmentStoreGetMetadataResponse
    {
        public long? LastUpToDate { get; set; }
    }

    public class BigSegmentStoreGetMembershipParams
    {
        public string ContextHash { get; set; }
    }

    public class BigSegmentStoreGetMembershipResponse
    {
        public Dictionary<string, bool?> Values { get; set; }
    }
}
