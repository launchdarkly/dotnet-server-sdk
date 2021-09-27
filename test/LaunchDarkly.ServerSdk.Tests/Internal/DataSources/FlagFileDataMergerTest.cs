using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class FlagFileDataMergerTest
    {
        [Fact]
        public void AddToData_DuplicateKeysHandling_Throw()
        {
            const string key = "flag1";

            FeatureFlag initialFeatureFlag = new FeatureFlagBuilder(key).Version(0).Build();

            var flagData = new Dictionary<string, ItemDescriptor>
            {
                 { key, new ItemDescriptor(1, initialFeatureFlag) }
            };
            var segmentData = new Dictionary<string, ItemDescriptor>();
            var fileData = new DataSetBuilder()
                .Flags(new FeatureFlagBuilder(key).Version(1).Build()).Build();

            FlagFileDataMerger merger = new FlagFileDataMerger(FileDataTypes.DuplicateKeysHandling.Throw);

            Exception err = Assert.Throws<Exception>(() =>
            {
                merger.AddToData(fileData, flagData, segmentData);
            });
            Assert.Equal("in \"features\", key \"flag1\" was already defined", err.Message);

            ItemDescriptor postFeatureFlag = flagData[key];
            Assert.Same(initialFeatureFlag, postFeatureFlag.Item);
            Assert.Equal(1, postFeatureFlag.Version);
        }

        [Fact]
        public void AddToData_DuplicateKeysHandling_Ignore()
        {
            const string key = "flag1";

            FeatureFlag initialFeatureFlag = new FeatureFlagBuilder(key).Version(0).Build();

            var flagData = new Dictionary<string, ItemDescriptor>
            {
                { key, new ItemDescriptor(1, initialFeatureFlag) }
            };
            var segmentData = new Dictionary<string, ItemDescriptor>();
            var fileData = new DataSetBuilder()
                .Flags(new FeatureFlagBuilder(key).Version(1).Build()).Build();

            FlagFileDataMerger merger = new FlagFileDataMerger(FileDataTypes.DuplicateKeysHandling.Ignore);
            merger.AddToData(fileData, flagData, segmentData);

            ItemDescriptor postFeatureFlag = flagData[key];
            Assert.Same(initialFeatureFlag, postFeatureFlag.Item);
            Assert.Equal(1, postFeatureFlag.Version);
        }
    }
}
