
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Types that are used by the <see cref="IBigSegmentStore"/> interface.
    /// </summary>
    public static class BigSegmentStoreTypes
    {
        /// <summary>
        /// A query interface returned by <see cref="IBigSegmentStore.GetMembershipAsync(string)"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It is associated with a single user, and provides the ability to check whether that
        /// user is included in or excluded from any number of big segments.
        /// </para>
        /// <para>
        /// This is an immutable snapshot of the state for this user at the time
        /// <see cref="IBigSegmentStore.GetMembershipAsync(string)"/> was called. Calling
        /// <see cref="CheckMembership(string)"/> should not cause the state to be queried again.
        /// The object should be safe for concurrent access by multiple threads.
        /// </para>
        /// </remarks>
        public interface IMembership
        {
            /// <summary>
            /// Tests whether the user is explicitly included or explicitly excluded in the
            /// specified segment, or neither.
            /// </summary>
            /// <remarks>
            /// <para>
            /// The segment is identified by a <code>segmentRef</code> which is not the same as
            /// the segment key: it includes the key but also versioning information that the SDK
            /// will provide. The store implementation should not be concerned with the format of
            /// this.
            /// </para>
            /// <para>
            /// If the user is explicitly included (regardless of whether the user is also explicitly
            /// excluded or not-- that is, inclusion takes priority over exclusion), the method returns
            /// a <see langword="true"/> value.
            /// </para>
            /// <para>
            /// If the user is explicitly excluded, and is not explicitly included, the method returns
            /// a <see langword="false"/> value.
            /// </para>
            /// <para>
            /// If the user's status in the segment is undefined, the method returns
            /// <see langword="null"/>.
            /// </para>
            /// </remarks>
            /// <param name="segmentRef">a string representing the segment query</param>
            /// <returns>true/false membership state, or null if unspecified</returns>
            bool? CheckMembership(string segmentRef);
        }

        /// <summary>
        /// Values returned by <see cref="IBigSegmentStore.GetMetadataAsync"/>.
        /// </summary>
        public struct StoreMetadata
        {
            /// <summary>
            /// The timestamp of the last update to the BigSegmentStore, if known.
            /// </summary>
            public UnixMillisecondTime? LastUpToDate { get; set; }
        }
    }
}
