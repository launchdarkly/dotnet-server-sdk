using System;
using LaunchDarkly.Logging;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// The most basic properties of the SDK client that are available to all SDK component factories.
    /// </summary>
    public sealed class BasicConfiguration
    {
        /// <summary>
        /// The base logger for all SDK components to use.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Components should use the methods of the <a href="https://github.com/launchdarkly/dotnet-logging">LaunchDarkly.Logging</a>
        /// API to write log messages which will be output or discarded as appropriate by the logging
        /// framework. This is the main logger for the SDK; components that want to tag their log messages
        /// with a more specific logger name can use the <c>SubLogger</c> method: for instance, if the
        /// base logger's name is "LaunchDarkly.Sdk", an event-processing component could use
        /// <c>SubLogger("Events")</c> to get a logger whose name is "LaunchDarkly.Sdk.Events".
        /// </para>
        /// <para>
        /// This property will never be null; if logging is disabled, it will be set to a stub logger
        /// that produces no output.
        /// </para>
        /// </remarks>
        public Logger Logger { get; }

        /// <summary>
        /// True if the SDK was configured to be completely offline.
        /// </summary>
        public bool Offline { get; }

        /// <summary>
        /// The configured SDK key.
        /// </summary>
        public string SdkKey { get; }

        /// <summary>
        /// Defines the base service URIs used by SDK components.
        /// </summary>
        public ServiceEndpoints ServiceEndpoints { get; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="sdkKey">the SDK key</param>
        /// <param name="offline">true if the SDK was configured to be completely offline</param>
        /// <param name="logger">the base logger for all SDK components to use</param>
        [Obsolete("Use all-parameters constructor")]
        public BasicConfiguration(string sdkKey, bool offline, Logger logger) :
            this(sdkKey, offline, null, logger) { }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="sdkKey">the SDK key</param>
        /// <param name="offline">true if the SDK was configured to be completely offline</param>
        /// <param name="serviceEndpoints">configured service URIs</param>
        /// <param name="logger">the base logger for all SDK components to use</param>
        public BasicConfiguration(string sdkKey, bool offline, ServiceEndpoints serviceEndpoints, Logger logger)
        {
            SdkKey = sdkKey;
            Offline = offline;
            ServiceEndpoints = serviceEndpoints ?? Components.ServiceEndpoints().Build();
            Logger = logger;
        }

        internal BasicConfiguration(Configuration config, Logger logger) :
            this(config.SdkKey, config.Offline, config.ServiceEndpoints, logger) { }
    }
}
