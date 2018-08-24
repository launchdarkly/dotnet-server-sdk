using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Objects used by <see cref="IFeatureStore"/> implementations to denote a specific collection of IVersionedData-derived objects.
    /// </summary>
    public interface IVersionedDataKind
    {
        /// <summary>
        /// An alphabetic string that distinguishes this collection from others, e.g. "features".
        /// </summary>
        string GetNamespace();

        /// <summary>
        /// The runtime class of objects in this collection, e.g. typeof(<c>FeatureFlag</c>).
        /// </summary>
        Type GetItemType();

        /// <summary>
        /// Used internally to identify streaming API requests for objects of this type.
        /// </summary>
        String GetStreamApiPath();
    }

    /// <summary>
    /// The members of this class denote all the <c>VersionedDataKind</c> collections that exist.
    /// </summary>
    internal abstract class VersionedDataKind
    {
        internal static VersionedDataKind<FeatureFlag> Features = new FeaturesVersionedDataKind();
        internal static VersionedDataKind<Segment> Segments = new SegmentsVersionedDataKind();
    }

    /// <summary>
    /// Objects used by <see cref="IFeatureStore"/> implementations to denote a specific collection of
    /// <c>IVersionedData</c>-derived objects.
    /// </summary>
    public abstract class VersionedDataKind<T> : IVersionedDataKind where T : IVersionedData
    {
        /// <see cref="IVersionedDataKind.GetNamespace"/>
        public abstract string GetNamespace();

        /// <see cref="IVersionedDataKind.GetItemType"/>
        public abstract Type GetItemType();

        /// <see cref="IVersionedDataKind.GetStreamApiPath"/>
        public abstract String GetStreamApiPath();

        /// <summary>
        /// Returns an instance of the desired class with the <c>Deleted</c> property set to
        /// true and the <c>Key</c> and <c>Version</c> properties prepopulated.
        /// </summary>
        /// <param name="key">the item's unique string key</param>
        /// <param name="version">the desired version number</param>
        /// <returns>an instance of the desired class</returns>
        public abstract T MakeDeletedItem(string key, int version);
    }

    internal class FeaturesVersionedDataKind : VersionedDataKind<FeatureFlag>
    {
        override public string GetNamespace()
        {
            return "features";
        }

        override public Type GetItemType()
        {
            return typeof(FeatureFlag);
        }

        public override string GetStreamApiPath()
        {
            return "/flags/";
        }

        public override FeatureFlag MakeDeletedItem(string key, int version)
        {
            return new FeatureFlag(key, version, false, null, "", null, null, null, null, null, false, null, true, false);
        }
    }

    internal class SegmentsVersionedDataKind : VersionedDataKind<Segment>
    {
        override public string GetNamespace()
        {
            return "segments";
        }

        override public Type GetItemType()
        {
            return typeof(Segment);
        }

        public override string GetStreamApiPath()
        {
            return "/segments/";
        }

        public override Segment MakeDeletedItem(string key, int version)
        {
            return new Segment(key, version, null, null, "", null, true);
        }
    }
}
