using System;
using System.Collections.Generic;
using LaunchDarkly.Client;
using LaunchDarkly.Client.Files;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class FlagFileDataTest
    {
        [Fact]
        public void AddToData_DuplicateKeysHandling_Throw()
        {
            const string key = "flag1";

            FeatureFlag initialFeatureFlag = new FeatureFlag(key, version: 0, deleted: false);

            Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> data =
                new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>
                {
                    {
                        VersionedDataKind.Features,
                        new Dictionary<string, IVersionedData>
                        {
                            { key, initialFeatureFlag}
                        }
                    }
                };

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

            Exception err = Assert.Throws<Exception>(() =>
            {
                fileData.AddToData(data, DuplicateKeysHandling.Throw);
            });
            Assert.Equal("in \"features\", key \"flag1\" was already defined", err.Message);

            IVersionedData postFeatureFlag = data[VersionedDataKind.Features][key];
            Assert.Same(initialFeatureFlag, postFeatureFlag);
            Assert.Equal(0, postFeatureFlag.Version);
        }

        [Fact]
        public void AddToData_DuplicateKeysHandling_Ignore()
        {
            const string key = "flag1";

            FeatureFlag initialFeatureFlag = new FeatureFlag(key, version: 0, deleted: false);

            Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> data =
                new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>
                {
                    {
                        VersionedDataKind.Features,
                        new Dictionary<string, IVersionedData>
                        {
                            { key, initialFeatureFlag}
                        }
                    }
                };

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

            fileData.AddToData(data, DuplicateKeysHandling.Ignore);

            IVersionedData postFeatureFlag = data[VersionedDataKind.Features][key];
            Assert.Same(initialFeatureFlag, postFeatureFlag);
            Assert.Equal(0, postFeatureFlag.Version);
        }
    }
}
