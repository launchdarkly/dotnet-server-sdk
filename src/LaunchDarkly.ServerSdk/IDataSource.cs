using System;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Interface for an object that receives updates to feature flags, user segments, and anything
    /// else that might come from LaunchDarkly, and passes them to an <see cref="IDataStore"/>.
    /// </summary>
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

    /// <summary>
    /// Used when the client is offline or in LDD mode.
    /// </summary>
    internal class NullDataSource : IDataSource
    {
        Task<bool> IDataSource.Start()
        {
            return Task.FromResult(true);
        }

        bool IDataSource.Initialized()
        {
            return true;
        }

        void IDisposable.Dispose() { }
    }

    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IDataSource"/>.
    /// </summary>
    /// <seealso cref="IConfigurationBuilder.DataSource"/>
    /// <seealso cref="Components"/>
    public interface IDataSourceFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <param name="config">the LaunchDarkly configuration</param>
        /// <param name="dataStore">the store that holds feature flags and related data</param>
        /// <returns>an <see cref="IDataSource"/> instance</returns>
        IDataSource CreateDataSource(Configuration config, IDataStore dataStore);
    }
}