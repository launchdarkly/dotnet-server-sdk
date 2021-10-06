using System;
using System.Threading.Tasks;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a read-only data store that allows querying of user membership in Big Segments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Big Segments" are a specific type of user segments. For more information, read the LaunchDarkly
    /// documentation about user segments: https://docs.launchdarkly.com/home/users/segments
    /// </para>
    /// <para>
    /// All query methods of the store are asynchronous.
    /// </para>
    /// </remarks>
    public interface IBigSegmentStore : IDisposable
    {
        /// <summary>
        /// Queries the store for a snapshot of the current segment state for a specific user.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The userHash is a base64-encoded string produced by hashing the user key as defined by
        /// the Big Segments specification; the store implementation does not need to know the details
        /// of how this is done, because it deals only with already-hashed keys, but the string can be
        /// assumed to only contain characters that are valid in base64.
        /// </para>
        /// <para>
        /// If the store is working, but no membership state is found for this user, the method may
        /// return either <see langword="null"/> or an empty <see cref="BigSegmentStoreTypes.IMembership"/>.
        /// It should not throw an exception unless there is an unexpected database error or the retrieved
        /// data is malformed.
        /// </para>
        /// </remarks>
        /// <param name="userHash">the hashed user identifier</param>
        /// <returns>the user's segment membership state or null</returns>
        Task<BigSegmentStoreTypes.IMembership> GetMembershipAsync(string userHash);

        /// <summary>
        /// Returns information about the overall state of the store.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method will be called only when the SDK needs the latest state, so it should not be cached.
        /// </para>
        /// <para>
        /// If the store is working, but no metadata has been stored in it yet, the method should return
        /// <see langword="null"/>. It should not throw an exception unless there is an unexpected database
        /// error or the retrieved data is malformed.
        /// </para>
        /// </remarks>
        /// <returns>the store metadata or null</returns>
        Task<BigSegmentStoreTypes.StoreMetadata?> GetMetadataAsync();
    }
}
