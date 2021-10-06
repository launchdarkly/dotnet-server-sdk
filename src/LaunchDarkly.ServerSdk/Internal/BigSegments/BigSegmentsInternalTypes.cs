using System;
using System.Security.Cryptography;
using System.Text;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.BigSegments
{
    internal static class BigSegmentsInternalTypes
    {
        private static readonly SHA256 _hasher = SHA256.Create();

        internal static string BigSegmentUserKeyHash(string userKey) =>
            Convert.ToBase64String(
                _hasher.ComputeHash(Encoding.UTF8.GetBytes(userKey))
                );

        internal static string MakeBigSegmentRef(Segment s) =>
            // The format of Big Segment references is independent of what store implementation is being
            // used; the store implementation receives only this string and does not know the details of
            // the data model. The Relay Proxy will use the same format when writing to the store.
            string.Format("{0}.g{1}", s.Key, s.Generation.Value);

        // This type is used when Evaluator is querying Big Segments
        internal struct BigSegmentsQueryResult
        {
            internal IMembership Membership { get; set; }
            internal BigSegmentsStatus Status { get; set; }
        }
    }
}
