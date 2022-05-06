using System.Collections.Generic;
using System.Threading.Tasks;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace TestService
{
    public class BigSegmentStoreFixture : IBigSegmentStore, IBigSegmentStoreFactory
    {
        private readonly CallbackService _service;

        public BigSegmentStoreFixture(CallbackService service)
        {
            _service = service;
        }

        public IBigSegmentStore CreateBigSegmentStore(LdClientContext context) => this;

        public void Dispose() =>
            _service.Close();

        public async Task<BigSegmentStoreTypes.IMembership> GetMembershipAsync(string contextHash)
        {
            var resp = await _service.PostAsync<BigSegmentStoreGetMembershipResponse>("/getMembership",
                new BigSegmentStoreGetMembershipParams { ContextHash = contextHash });
            return resp == null ? (BigSegmentStoreTypes.IMembership)null : new MembershipImpl { Values = resp.Values };
        }

        public async Task<BigSegmentStoreTypes.StoreMetadata?> GetMetadataAsync()
        {
            var resp = await _service.PostAsync<BigSegmentStoreGetMetadataResponse>("/getMetadata", null);
            return new BigSegmentStoreTypes.StoreMetadata() { LastUpToDate =
                resp.LastUpToDate.HasValue ? UnixMillisecondTime.OfMillis(resp.LastUpToDate.Value) :
                    (UnixMillisecondTime?)null };
        }

        private class MembershipImpl : BigSegmentStoreTypes.IMembership
        {
            public Dictionary<string, bool?> Values { get; set; }

            public bool? CheckMembership(string segmentRef) =>
                Values == null ? (bool?)null :
                    (Values.TryGetValue(segmentRef, out var value) ? value : (bool?)null);
        }
    }
}
