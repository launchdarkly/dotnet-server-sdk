using System;
using System.Threading.Tasks;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Interface for an object that receives updates to feature flags, user segments, and anything
    /// else that might come from LaunchDarkly, and passes them to an <see cref="IFeatureStore"/>.
    /// </summary>
    /// <seealso cref="IUpdateProcessorFactory"/>
    public interface IUpdateProcessor : IDisposable
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
    internal class NullUpdateProcessor : IUpdateProcessor
    {
        Task<bool> IUpdateProcessor.Start()
        {
            return Task.FromResult(true);
        }

        bool IUpdateProcessor.Initialized()
        {
            return true;
        }

        void IDisposable.Dispose() { }
    }

    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IUpdateProcessor"/>.
    /// </summary>
    /// <seealso cref="ConfigurationExtensions.WithUpdateProcessorFactory"/>
    /// <seealso cref="Components"/>
    public interface IUpdateProcessorFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <param name="config">the LaunchDarkly configuration</param>
        /// <param name="featureStore">the store that holds feature flags and related data</param>
        /// <returns>an <c>IUpdateProcessor</c> instance</returns>
        IUpdateProcessor CreateUpdateProcessor(Configuration config, IFeatureStore featureStore);
    }

    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IUpdateProcessor"/>,
    /// with support for interfacing with a diagnostic store.
    /// </summary>
    /// <seealso cref="ConfigurationExtensions.WithUpdateProcessorFactory"/>
    /// <seealso cref="Components"/>
    internal interface IUpdateProcessorFactoryWithDiagnostics : IUpdateProcessorFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <param name="config">the LaunchDarkly configuration</param>
        /// <param name="featureStore">the store that holds feature flags and related data</param>
        /// <param name="diagnosticStore">the diagnostic store</param>
        /// <returns>an <c>IUpdateProcessor</c> instance</returns>
        IUpdateProcessor CreateUpdateProcessor(Configuration config, IFeatureStore featureStore, IDiagnosticStore diagnosticStore);
    }
}