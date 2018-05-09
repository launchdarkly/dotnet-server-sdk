using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Defines basic properties that differ between the server-side and mobile clients.
    /// </summary>
    internal abstract class ClientEnvironment
    {
        /// <summary>
        /// The assembly version string.
        /// </summary>
        public string Version
        {
            get
            {
                Type thisType = this.GetType();
                // Note, this is the type of the concrete subclass, not the base ClientEnvironment.
                // Therefore we can use it to get information about whichever client assembly it belongs to.
                var infoAttr = (AssemblyInformationalVersionAttribute)thisType.GetTypeInfo().Assembly
                    .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute));
                return infoAttr.InformationalVersion;
            }
        }

        /// <summary>
        /// The part of the User-Agent header before the slash.
        /// </summary>
        public abstract string UserAgentType { get; }
    }
}
