using System;
using System.Collections.Generic;
using System.Linq;

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
    /// This interface is implemented by <see cref="IVersionedDataKind"/> instances that
    /// specify a preferred ordering for data updates.
    /// </summary>
    public interface IVersionedDataOrdering
    {
        /// <summary>
        /// Data sets with a lower value of this property will be updated first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Returns the keys of items within this data set that are prerequisites of the specified item.
        /// </summary>
        /// <param name="item">an item that may have prerequisites</param>
        /// <returns>prerequisite keys of the item</returns>
        IEnumerable<string> GetDependencyKeys(IVersionedData item);
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

    internal abstract class Impl<T> : VersionedDataKind<T>, IVersionedDataOrdering where T : IVersionedData
    {
        private readonly string _namespace;
        private readonly Type _itemType;
        private readonly string _streamApiPath;
        private readonly int _priority;

        internal Impl(string ns, Type itemType, string streamApiPath, int priority)
        {
            _namespace = ns;
            _itemType = itemType;
            _streamApiPath = streamApiPath;
            _priority = priority;
        }

        public override string GetNamespace() => _namespace;
        public override Type GetItemType() => _itemType;
        public override string GetStreamApiPath() => _streamApiPath;
        public int Priority => _priority;

        public virtual IEnumerable<string> GetDependencyKeys(IVersionedData item)
        {
            return Enumerable.Empty<string>();
        }
    }

    internal class FeaturesVersionedDataKind : Impl<FeatureFlag>
    {
        internal FeaturesVersionedDataKind() : base("features", typeof(FeatureFlag), "/flags/", 1) { }
        
        public override FeatureFlag MakeDeletedItem(string key, int version)
        {
            return new FeatureFlag(key, version, true);
        }

        public override IEnumerable<string> GetDependencyKeys(IVersionedData item)
        {
            var ps = ((item as FeatureFlag).Prerequisites) ?? Enumerable.Empty<Prerequisite>();
            return from p in ps select p.Key;
        }
    }

    internal class SegmentsVersionedDataKind : Impl<Segment>
    {
        internal SegmentsVersionedDataKind() : base("segments", typeof(Segment), "/segments/", 0) { }

        public override Segment MakeDeletedItem(string key, int version)
        {
            return new Segment(key, version, null, null, "", null, true);
        }
    }
}
