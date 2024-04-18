using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring wrapper information.
    /// <para>
    /// If the WrapperBuilder is used, then it will replace the wrapper information from the HttpConfigurationBuilder.
    /// </para>
    /// <para>
    /// Additionally, any wrapper SDK may overwrite any application developer provided wrapper information.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This builder is primarily intended for use by LaunchDarkly in developing wrapper SDKs.
    /// </remarks>
    public sealed class WrapperInfoBuilder
    {
        private string _name;
        private string _version;

        /// <summary>
        /// Set the name of the wrapper.
        /// </summary>
        /// <param name="value">the name of the wrapper</param>
        /// <returns>the builder</returns>
        public WrapperInfoBuilder Name(string value)
        {
            _name = value;
            return this;
        }

        /// <summary>
        /// Set the version of the wrapper.
        /// </summary>
        /// <param name="value">the version of the wrapper</param>
        /// <returns>the builder</returns>
        public WrapperInfoBuilder Version(string value)
        {
            _version = value;
            return this;
        }

        /// <summary>
        /// Called internally by the SDK to create a configuration instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <returns>the wrapper information</returns>
        public WrapperInfo Build()
        {
            return new WrapperInfo(_name, _version);
        }
    }
}
