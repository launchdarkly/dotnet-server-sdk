using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.Sdk.Server.Hooks;
using LaunchDarkly.Sdk.Server.Subsystems;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// HookConfigurationBuilder is a builder for the SDK's hook configuration.
    /// </summary>
    public sealed class HookConfigurationBuilder
    {
        private readonly List<Hook> _hooks;

        /// <summary>
        /// Constructs a configuration representing no hooks by default.
        /// </summary>
        public HookConfigurationBuilder() : this(new List<Hook>())
        {
        }

        /// <summary>
        /// Constructs a configuration from an existing collection of hooks.
        /// </summary>
        /// <param name="hooks">the collection of hooks</param>
        public HookConfigurationBuilder(IEnumerable<Hook> hooks)
        {
            _hooks = hooks.ToList();
        }

        /// <summary>
        /// Adds a hook to the configuration. Hooks are executed in the order they are added; see <see cref="Hook"/>.
        /// </summary>
        /// <param name="hook">the hook</param>
        /// <returns>the builder</returns>
        public HookConfigurationBuilder Add(Hook hook)
        {
            _hooks.Add(hook);
            return this;
        }

        /// <summary>
        /// Builds the configuration.
        /// </summary>
        /// <returns>the build configuration</returns>
        public HookConfiguration Build()
        {
            return new HookConfiguration(_hooks);
        }
    }
}
