using System;
using System.Threading.Tasks;

using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal interface IFeatureRequestor : IDisposable
    {
        Task<FullDataSet<ItemDescriptor>?> GetAllDataAsync();
    }
}
