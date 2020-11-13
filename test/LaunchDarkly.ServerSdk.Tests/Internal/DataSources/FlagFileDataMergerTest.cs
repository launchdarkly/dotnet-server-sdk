using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Newtonsoft.Json.Linq;
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

            FeatureFlag initialFeatureFlag = new FeatureFlag(key, version: 0, deleted: false);

            var flagData = new Dictionary<string, ItemDescriptor>
            {
                 { key, new ItemDescriptor(1, initialFeatureFlag) }
            };
            var segmentData = new Dictionary<string, ItemDescriptor>();
            FlagFileData fileData = new FlagFileData
            {
                Flags = new Dictionary<string, JToken>
                {
                    {
                        key,
                        new JObject(
                            new JProperty("key", key),
                            new JProperty("version", 1)
                        )
                    }
                }
            };

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

            FeatureFlag initialFeatureFlag = new FeatureFlag(key, version: 0, deleted: false);

            var flagData = new Dictionary<string, ItemDescriptor>
            {
                { key, new ItemDescriptor(1, initialFeatureFlag) }
            };
            var segmentData = new Dictionary<string, ItemDescriptor>();
            FlagFileData fileData = new FlagFileData
            {
                Flags = new Dictionary<string, JToken>
                {
                    {
                        key,
                        new JObject(
                            new JProperty("key", key),
                            new JProperty("version", 1)
                        )
                    }
                }
            };

            FlagFileDataMerger merger = new FlagFileDataMerger(FileDataTypes.DuplicateKeysHandling.Ignore);
            merger.AddToData(fileData, flagData, segmentData);

            ItemDescriptor postFeatureFlag = flagData[key];
            Assert.Same(initialFeatureFlag, postFeatureFlag.Item);
            Assert.Equal(1, postFeatureFlag.Version);
        }
    }
}
