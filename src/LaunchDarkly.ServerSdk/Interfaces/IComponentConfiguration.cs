
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// The common interface for SDK component factories and configuration builders. Applications should not
    /// need to implement this interface.
    /// </summary>
    /// <typeparam name="T">the type of SDK component or configuration object being constructed</typeparam>
    public interface IComponentConfiguration<T>
    {
        /// <summary>
        /// Called internally by the SDK to create an implementation instance. Applications should not need
        /// to call this method.
        /// </summary>
        /// <param name="context">provides configuration properties and other components from the current
        /// SDK client instance</param>
        /// <returns>a instance of the component type</returns>
        T Build(LdClientContext context);
    }
}
