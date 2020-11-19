using System;
using System.Threading.Tasks;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for an object that receives updates to feature flags, user segments, and anything
    /// else that might come from LaunchDarkly.
    /// </summary>
    /// <remarks>
    /// This component uses a push model. When it is created (via <see cref="IDataSourceFactory"/>) it is
    /// given a reference to an <see cref="IDataStoreUpdates"/> component, which is a write-only abstraction of
    /// the data store. The SDK never requests feature flag data from the <see cref="IDataSource"/>, it
    /// only looks at the last known data that was previously put into the store.
    /// </remarks>
    /// <seealso cref="IDataSourceFactory"/>
    public interface IDataSource : IDisposable
    {
        /// <summary>
        /// Initializes the processor. This is called once from the <see cref="LdClient"/> constructor.
        /// </summary>
        /// <returns>a <c>Task</c> which is completed once the processor has finished starting up</returns>
        Task<bool> Start();
        
        /// <summary>
        /// Checks whether the processor has finished initializing.
        /// </summary>
        /// <returns>true if fully initialized</returns>
        bool Initialized();
    }
}