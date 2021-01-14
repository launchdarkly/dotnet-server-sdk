
namespace LaunchDarkly.Client.Interfaces
{
    /// <summary>
    /// Optional interface for components to describe their own configuration.
    /// </summary>
    internal interface IDiagnosticDescription
    {
        LdValue DescribeConfiguration(Configuration config);
    }
}
