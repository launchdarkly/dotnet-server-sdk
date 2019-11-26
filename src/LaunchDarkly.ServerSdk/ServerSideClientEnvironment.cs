using LaunchDarkly.Sdk.Internal;

namespace LaunchDarkly.Sdk.Server
{
    internal class ServerSideClientEnvironment : ClientEnvironment
    {
        internal static readonly ServerSideClientEnvironment Instance =
            new ServerSideClientEnvironment();
        
        public override string UserAgentType { get { return "DotNetClient";  } }
    }
}
