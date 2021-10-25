using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Internal.Events.DiagnosticConfigProperties;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    internal class ServerDiagnosticStore : DiagnosticStoreBase
    {
        private readonly Configuration _config;
        private readonly BasicConfiguration _basicConfig;
        private readonly HttpConfiguration _httpConfig;

        protected override string SdkKeyOrMobileKey => _config.SdkKey;
        protected override string SdkName => "dotnet-server-sdk";
        protected override IEnumerable<LdValue> ConfigProperties => GetConfigProperties();
        protected override string DotNetTargetFramework => GetDotNetTargetFramework();
        protected override HttpProperties HttpProperties => _httpConfig.HttpProperties;
        protected override Type TypeOfLdClient => typeof(LdClient);

        internal ServerDiagnosticStore(Configuration config, BasicConfiguration basicConfig, HttpConfiguration httpConfig)
        {
            _config = config;
            _basicConfig = basicConfig;
            _httpConfig = httpConfig;
        }

        private IEnumerable<LdValue> GetConfigProperties()
        {
            yield return LdValue.BuildObject()
                .WithStartWaitTime(_config.StartWaitTime)
                .Build();

            // Allow each pluggable component to describe its own relevant properties.
            yield return GetComponentDescription(_config.DataStoreFactory ?? Components.InMemoryDataStore, "dataStoreType");
            yield return GetComponentDescription(_config.DataSourceFactory ?? Components.StreamingDataSource());
            yield return GetComponentDescription(_config.EventProcessorFactory ?? Components.SendEvents());
            yield return GetComponentDescription(_config.HttpConfigurationFactory ?? Components.HttpConfiguration());
        }

        private LdValue GetComponentDescription(object component, string componentName = null)
        {
            if (component is IDiagnosticDescription dd)
            {
                var componentDesc = dd.DescribeConfiguration(_basicConfig);
                if (componentName is null)
                {
                    return componentDesc;
                }
                if (componentDesc.IsString)
                {
                    return LdValue.BuildObject().Add(componentName, componentDesc).Build();
                }
            }
            if (componentName != null)
            {
                return LdValue.BuildObject().Add(componentName, "custom").Build();
            }
            return LdValue.Null;
        }

        internal static string GetDotNetTargetFramework()
        {
            // Note that this is the _target framework_ that was selected at build time based on the application's
            // compatibility requirements; it doesn't tell us anything about the actual OS version. We'll need to
            // update this whenever we add or remove supported target frameworks in the .csproj file.
#if NETSTANDARD2_0
            return "netstandard2.0";
#elif NETCOREAPP2_1
            return "netcoreapp2.1";
#elif NET452
            return "net452";
#elif NET471
            return "net471";
#elif NET5_0
            return "net5.0";
#else
            return "unknown";
#endif
        }
    }
}
