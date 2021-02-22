using System.Collections.Generic;
using LaunchDarkly.JsonStream;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// Contains information about the internal data model for feature flags and user segments.
    /// </summary>
    /// <remarks>
    /// The details of the data model are not public to application code (although of course developers can easily
    /// look at the code or the data) so that changes to LaunchDarkly SDK implementation details will not be breaking
    /// changes to the application. Therefore, most of the members of this class are internal. The public members
    /// provide a high-level description of model objects so that custom integration code or test code can store or
    /// serialize them.
    /// </remarks>
    public static class DataModel
    {
        /// <summary>
        /// The <see cref="DataKind"/> instance that describes feature flag data.
        /// </summary>
        /// <remarks>
        /// Applications should not need to reference this object directly. It is public so that custom integrations
        /// and test code can serialize or deserialize data or inject it into a data store.
        /// </remarks>
        public static DataKind Features = new DataKind("features", SerializeFlag, DeserializeFlag);

        /// <summary>
        /// The <see cref="DataKind"/> instance that describes user segment data.
        /// </summary>
        /// <remarks>
        /// Applications should not need to reference this object directly. It is public so that custom integrations
        /// and test code can serialize or deserialize data or inject it into a data store.
        /// </remarks>
        public static DataKind Segments = new DataKind("segments", SerializeSegment, DeserializeSegment);

        /// <summary>
        /// An enumeration of all supported <see cref="DataKind"/>s.
        /// </summary>
        /// <remarks>
        /// Applications should not need to reference this object directly. It is public so that custom data store
        /// implementations can determine ahead of time what kinds of model objects may need to be stored, if
        /// necessary.
        /// </remarks>
        public static IEnumerable<DataKind> AllDataKinds
        {
            get
            {
                yield return Features;
                yield return Segments;
            }
        }

        private static void SerializeFlag(object o, IValueWriter w) =>
            FeatureFlagSerialization.Instance.WriteJson(o as FeatureFlag, w);

        private static ItemDescriptor DeserializeFlag(ref JReader r)
        {
            var flag = FeatureFlagSerialization.Instance.ReadJson(ref r) as FeatureFlag;
            return flag.Deleted ? ItemDescriptor.Deleted(flag.Version) :
                new ItemDescriptor(flag.Version, flag);
        }

        private static void SerializeSegment(object o, IValueWriter w) =>
            SegmentSerialization.Instance.WriteJson(o as Segment, w);

        private static ItemDescriptor DeserializeSegment(ref JReader r)
        {
            var segment = SegmentSerialization.Instance.ReadJson(ref r) as Segment;
            return segment.Deleted ? ItemDescriptor.Deleted(segment.Version) :
                new ItemDescriptor(segment.Version, segment);
        }
    }
}
