using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Hooks;

namespace LaunchDarkly.Sdk.Server.Subsystems
{
    /// <summary>
    /// HookConfiguration represents the hooks that will be executed by the SDK at runtime.
    /// </summary>
    public sealed class HookConfiguration
    {
        /// <summary>
        /// Collection of hooks.
        /// </summary>
        public IEnumerable<Hook> Hooks { get; }

        /// <summary>
        /// Constructs a new configuration from a collection of hooks.
        /// </summary>
        /// <param name="hooks">the collection of hooks</param>
        public HookConfiguration(IEnumerable<Hook> hooks)
        {
            Hooks = hooks;
        }
    }
}
