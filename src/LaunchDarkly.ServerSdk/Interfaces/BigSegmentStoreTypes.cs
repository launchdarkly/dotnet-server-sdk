using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.BigSegments;

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
        /// user is included in or excluded from any number of Big Segments.
        /// </para>
        /// <para>
        /// This is an immutable snapshot of the state for this user at the time
        /// <see cref="IBigSegmentStore.GetMembershipAsync(string)"/> was called. Calling
        /// <see cref="CheckMembership(string)"/> should not cause the state to be queried again.
        /// The object should be safe for concurrent access by multiple threads.
        /// </para>
        /// </remarks>
        /// <seealso cref="NewMembershipFromSegmentRefs(IEnumerable{string}, IEnumerable{string})"/>
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
        /// Convenience method for creating an implementation of <see cref="IMembership"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is intended to be used by Big Segment store implementations; application code
        /// does not need to use it.
        /// </para>
        /// <para>
        /// Store implementations are free to implement <see cref="IMembership"/> in any way that they
        /// find convenient and efficient, depending on what format they obtain values in from the
        /// database, but this method provides a simple way to do it as long as there are enumerations
        /// of included and excluded segment references. As described in <see cref="IMembership"/>, a
        /// <code>segmentRef</code> is not the same as the key property in the segment data model; it
        /// includes the key but also versioning information that the SDK will provide. The store
        /// implementation should not be concerned with the format of this.
        /// </para>
        /// <para>
        /// The returned object's <see cref="IMembership.CheckMembership(string)"/> method will return
        /// <see langword="true"/> for any <code>segmentRef</code> that is in the included list,
        /// <see langword="false"/> for any <code>segmentRef</code> that is in the excluded list and
        /// not also in the included list (that is, inclusions override exclusions), and
        /// <see langword="null"/> for all others.
        /// </para>
        /// <para>
        /// The method is optimized to return a singleton empty membership object whenever the
        /// inclusion and exclusion lists are both empty.
        /// </para>
        /// <para>
        /// The returned object implements <see cref="Object.Equals(object)"/> in such a way that it
        /// correctly tests equality when compared to any object returned from this factory method,
        /// but is always unequal to any other types of objects.
        /// </para>
        /// </remarks>
        /// <param name="includedSegmentRefs">the inclusion list (null is equivalent to an empty
        /// enumeration)</param>
        /// <param name="excludedSegmentRefs">the exclusion list (null is equivalent to an empty
        /// enumeration)</param>
        /// <returns>an <see cref="IMembership"/></returns>
        public static IMembership NewMembershipFromSegmentRefs(
            IEnumerable<string> includedSegmentRefs,
            IEnumerable<string> excludedSegmentRefs
            )
        {
            var builder = new MembershipBuilder();
            builder.AddRefs(excludedSegmentRefs, false); // add excludes first so includes will override them
            builder.AddRefs(includedSegmentRefs, true);
            return builder.Build();
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
