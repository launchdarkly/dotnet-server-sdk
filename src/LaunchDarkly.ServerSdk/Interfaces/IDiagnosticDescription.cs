
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Optional interface for components to describe their own configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The SDK uses a simplified JSON representation of its configuration when recording diagnostics data.
    /// Any class that implements <see cref="IDataStoreFactory"/>, <see cref="IDataSourceFactory"/>,
    /// <see cref="IEventProcessorFactory"/>, or <see cref="IPersistentDataStoreFactory"/> may choose to
    /// contribute values to this representation, although the SDK may or may not use them.
    /// </para>
    /// <para>
    /// The <see cref="DescribeConfiguration"/> method should return either <see cref="LdValue.Null"/>or a
    /// JSON value. For custom components, the value must be a string that describes the basic nature of
    /// this component implementation (e.g. "Redis"). Built-in LaunchDarkly components may instead return a
    /// JSON object containing multiple properties specific to the LaunchDarkly diagnostic schema.
    /// </para>
    /// </remarks>
    public interface IDiagnosticDescription
    {
        /// <summary>
        /// Called internally by the SDK to inspect the configuration. Applications do not need to call
        /// this method.
        /// </summary>
        /// <param name="basic">the basic global configuration of the SDK</param>
        /// <returns>a JSON value</returns>
        LdValue DescribeConfiguration(BasicConfiguration basic);
    }
}
