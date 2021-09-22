
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Information about a data store status change.
    /// </summary>
    /// <seealso cref="IDataStoreStatusProvider"/>
    public struct DataStoreStatus
    {
        /// <summary>
        /// True if the SDK believes the data store is now available.
        /// </summary>
        /// <remarks>
        /// This property is normally true. If the SDK receives an exception while trying to query
        /// or update the data store, then it sets this property to false (notifying listeners, if
        /// any) and polls the store at intervals until a query succeeds. Once it succeeds, it sets
        /// the property back to true (again notifying listeners).
        /// </remarks>
        public bool Available { get; set; }

        /// <summary>
        /// True if the store may be out of date due to a previous outage, so the SDK should attempt
        /// to refresh all feature flag data and rewrite it to the store.
        /// </summary>
        /// <remarks>
        /// This property is not meaningful to application code. It is used internally.
        /// </remarks>
        public bool RefreshNeeded { get; set; }

        /// <inheritdoc/>
        public override string ToString() =>
            string.Format("DataStoreStatus({0},{1})", Available, RefreshNeeded);
    }
}
