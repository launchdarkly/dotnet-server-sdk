namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Contains wrapper SDK information.
    /// </summary>
    /// <remarks>
    /// This class's properties are not public, since they are only read by the SDK.
    /// </remarks>
    /// <seealso cref="LaunchDarkly.Sdk.Server.Integrations.WrapperInfoBuilder"/>
    public sealed class WrapperInfo
    {
        internal string Name { get; }
        internal string Version { get; }

        internal WrapperInfo(
            string name,
            string version
        )
        {
            Name = name;
            Version = version;
        }
    }
}
