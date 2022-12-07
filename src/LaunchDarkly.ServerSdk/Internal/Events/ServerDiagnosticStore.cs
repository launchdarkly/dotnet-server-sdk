using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Subsystems;

using static LaunchDarkly.Sdk.Internal.Events.DiagnosticConfigProperties;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    internal class ServerDiagnosticStore : DiagnosticStoreBase
    {
        private readonly Configuration _config;
        private readonly LdClientContext _context;

        protected override string SdkKeyOrMobileKey => _config.SdkKey;
        protected override string SdkName => "dotnet-server-sdk";
        protected override IEnumerable<LdValue> ConfigProperties => GetConfigProperties();
        protected override string DotNetTargetFramework => GetDotNetTargetFramework();
        protected override HttpProperties HttpProperties => _context.Http.HttpProperties;
        protected override Type TypeOfLdClient => typeof(LdClient);

        internal ServerDiagnosticStore(Configuration config, LdClientContext context)
        {
            _config = config;
            _context = context;
        }

        private IEnumerable<LdValue> GetConfigProperties()
        {
            yield return LdValue.BuildObject()
                .WithStartWaitTime(_config.StartWaitTime)
                .Build();

            // Allow each pluggable component to describe its own relevant properties.
            yield return GetComponentDescription(_config.DataStore ?? Components.InMemoryDataStore, "dataStoreType");
            yield return GetComponentDescription(_config.DataSource ?? Components.StreamingDataSource());
            yield return GetComponentDescription(_config.Events ?? Components.SendEvents());
            yield return GetComponentDescription(_config.Http ?? Components.HttpConfiguration());
        }

        private LdValue GetComponentDescription(object component, string componentName = null)
        {
            if (component is IDiagnosticDescription dd)
            {
                var componentDesc = dd.DescribeConfiguration(_context);
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
#elif NETCOREAPP3_1
            return "netcoreapp3.1";
#elif NET462
            return "net462";
#elif NET6_0
            return "net6.0";
#else
            return "unknown";
#endif
        }
    }
}
