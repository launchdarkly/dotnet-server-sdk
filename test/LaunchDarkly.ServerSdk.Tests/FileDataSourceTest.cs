using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;
using LaunchDarkly.Client;
using LaunchDarkly.Client.Files;
using YamlDotNet.Serialization;

namespace LaunchDarkly.Tests
{
    public class FileDataSourceTest
    {
        private static readonly string ALL_DATA_JSON_FILE = TestUtils.TestFilePath("all-properties.json");
        private static readonly string ALL_DATA_YAML_FILE = TestUtils.TestFilePath("all-properties.yml");

        private readonly IFeatureStore store = TestUtils.InMemoryFeatureStore();
        private readonly FileDataSourceFactory factory = FileComponents.FileDataSource();
        private readonly Configuration config = Configuration.Builder("sdkKey")
            .EventProcessorFactory(Components.NullEventProcessor)
            .Build();
        private readonly User user = User.WithKey("key");

        [Fact]
        public void FlagsAreNotLoadedUntilStart()
        {
            factory.WithFilePaths(ALL_DATA_JSON_FILE);
            using (var fp = factory.CreateUpdateProcessor(config, store))
            {
                Assert.False(store.Initialized());
                Assert.Equal(0, CountFlagsInStore());
                Assert.Equal(0, CountSegmentsInStore());
            }
        }

        [Fact]
        public void FlagsAreLoadedOnStart()
        {
            factory.WithFilePaths(ALL_DATA_JSON_FILE);
            using (var fp = factory.CreateUpdateProcessor(config, store))
            {
                fp.Start();
                Assert.True(store.Initialized());
                Assert.Equal(2, CountFlagsInStore());
                Assert.Equal(1, CountSegmentsInStore());
            }
        }

        [Fact]
        public void FlagsCanBeLoadedWithExternalYamlParser()
        {
            var yaml = new DeserializerBuilder().Build();
            factory.WithFilePaths(ALL_DATA_YAML_FILE)
                .WithParser(s => yaml.Deserialize<object>(s));
            using (var fp = factory.CreateUpdateProcessor(config, store))
            {
                fp.Start();
                Assert.True(store.Initialized());
                Assert.Equal(2, CountFlagsInStore());
                Assert.Equal(1, CountSegmentsInStore());
            }
        }

        [Fact]
        public void StartTaskIsCompletedAndInitializedIsTrueAfterSuccessfulLoad()
        {
            using (var fp = factory.CreateUpdateProcessor(config, store))
            {
                var task = fp.Start();
                Assert.True(task.IsCompleted);
                Assert.True(fp.Initialized());
            }
        }

        [Fact]
        public void StartTaskIsCompletedAndInitializedIsFalseAfterFailedLoadDueToMissingFile()
        {
            factory.WithFilePaths(ALL_DATA_JSON_FILE, "bad-file-path");
            using (var fp = factory.CreateUpdateProcessor(config, store))
            {
                var task = fp.Start();
                Assert.True(task.IsCompleted);
                Assert.False(fp.Initialized());
            }
        }

        [Fact]
        public void CanIgnoreMissingFileOnStartup()
        {
            factory.WithFilePaths(ALL_DATA_JSON_FILE, "bad-file-path").WithSkipMissingPaths(true);
            using (var fp = factory.CreateUpdateProcessor(config, store))
            {
                var task = fp.Start();
                Assert.True(task.IsCompleted);
                Assert.True(fp.Initialized());
                Assert.Equal(2, CountFlagsInStore());
            }
        }

        [Fact]
        public void StartTaskIsCompletedAndInitializedIsFalseAfterFailedLoadDueToMalformedFile()
        {
            factory.WithFilePaths(TestUtils.TestFilePath("bad-file.txt"));
            using (var fp = factory.CreateUpdateProcessor(config, store))
            {
                var task = fp.Start();
                Assert.True(task.IsCompleted);
                Assert.False(fp.Initialized());
            }
        }
        
        [Fact]
        public void ModifiedFileIsNotReloadedIfAutoUpdateIsOff()
        {
            var filename = Path.GetTempFileName();
            factory.WithFilePaths(filename);
            try
            {
                File.WriteAllText(filename, File.ReadAllText(TestUtils.TestFilePath("flag-only.json")));
                using (var fp = factory.CreateUpdateProcessor(config, store))
                {
                    fp.Start();
                    File.WriteAllText(filename, File.ReadAllText(TestUtils.TestFilePath("segment-only.json")));
                    Thread.Sleep(TimeSpan.FromMilliseconds(400));
                    Assert.Equal(1, CountFlagsInStore());
                    Assert.Equal(0, CountSegmentsInStore());
                }
            }
            finally
            {
                File.Delete(filename);
            }
        }

        [Fact]
        public void ModifiedFileIsReloadedIfAutoUpdateIsOn()
        {
            var filename = Path.GetTempFileName();
            factory.WithFilePaths(filename).WithAutoUpdate(true).WithPollInterval(TimeSpan.FromMilliseconds(200));
            try
            {
                File.WriteAllText(filename, File.ReadAllText(TestUtils.TestFilePath("flag-only.json")));
                using (var fp = factory.CreateUpdateProcessor(config, store))
                {
                    fp.Start();
                    Assert.True(store.Initialized());
                    Assert.Equal(0, CountSegmentsInStore());

                    Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                    // See FilePollingReloader for the reason behind this long sleep

                    File.WriteAllText(filename, File.ReadAllText(TestUtils.TestFilePath("segment-only.json")));

                    Assert.True(
                        WaitForCondition(TimeSpan.FromSeconds(5), () => CountSegmentsInStore() == 1),
                        "Did not detect file modification"
                    );
                }
            }
            finally
            {
                File.Delete(filename);
            }
        }

        [Fact]
        public void ModifiedFileIsNotReloadedIfOneFileIsMissing()
        {
            var filename1 = Path.GetTempFileName();
            var filename2 = Path.GetTempFileName();
            factory.WithFilePaths(filename1, filename2)
                .WithAutoUpdate(true).WithPollInterval(TimeSpan.FromMilliseconds(200));
            try
            {
                File.WriteAllText(filename1, File.ReadAllText(TestUtils.TestFilePath("flag-only.json")));
                File.WriteAllText(filename2, "{}");
                using (var fp = factory.CreateUpdateProcessor(config, store))
                {
                    fp.Start();
                    Assert.True(store.Initialized());
                    Assert.Equal(0, CountSegmentsInStore());

                    Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                    // See FilePollingReloader for the reason behind this long sleep

                    File.Delete(filename2);
                    File.WriteAllText(filename1, File.ReadAllText(TestUtils.TestFilePath("segment-only.json")));

                    Thread.Sleep(TimeSpan.FromMilliseconds(400));
                    Assert.Equal(0, CountSegmentsInStore());
                }
            }
            finally
            {
                File.Delete(filename1);
                File.Delete(filename2);
            }
        }

        [Fact]
        public void ModifiedFileIsReloadedEvenIfOneFileIsMissingIfSkipMissingPathsIsSet()
        {
            var filename1 = Path.GetTempFileName();
            var filename2 = Path.GetTempFileName();
            File.Delete(filename2);
            factory.WithFilePaths(filename1, filename2)
                .WithSkipMissingPaths(true)
                .WithAutoUpdate(true).WithPollInterval(TimeSpan.FromMilliseconds(200));
            try
            {
                File.WriteAllText(filename1, File.ReadAllText(TestUtils.TestFilePath("flag-only.json")));
                using (var fp = factory.CreateUpdateProcessor(config, store))
                {
                    fp.Start();
                    Assert.True(store.Initialized());
                    Assert.Equal(0, CountSegmentsInStore());

                    Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                    // See FilePollingReloader for the reason behind this long sleep

                    File.WriteAllText(filename1, File.ReadAllText(TestUtils.TestFilePath("segment-only.json")));

                    Assert.True(
                        WaitForCondition(TimeSpan.FromSeconds(3), () => CountSegmentsInStore() == 1),
                        "Did not detect file modification"
                    );
                }
            }
            finally
            {
                File.Delete(filename1);
                File.Delete(filename2);
            }
        }

        [Fact]
        public void IfFlagsAreBadAtStartTimeAutoUpdateCanStillLoadGoodDataLater()
        {
            var filename = Path.GetTempFileName();
            factory.WithFilePaths(filename).WithAutoUpdate(true).WithPollInterval(TimeSpan.FromMilliseconds(200));
            try
            {
                File.WriteAllText(filename, "{not correct}");
                using (var fp = factory.CreateUpdateProcessor(config, store))
                {
                    fp.Start();
                    Assert.False(store.Initialized());

                    Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                    // See FilePollingReloader for the reason behind this long sleep

                    File.WriteAllText(filename, File.ReadAllText(TestUtils.TestFilePath("segment-only.json")));

                    Assert.True(
                        WaitForCondition(TimeSpan.FromSeconds(5), () => CountSegmentsInStore() == 1),
                        "Did not detect file modification"
                    );
                }
            }
            finally
            {
                File.Delete(filename);
            }
        }

        [Fact]
        public void FullFlagDefinitionEvaluatesAsExpected()
        {
            factory.WithFilePaths(ALL_DATA_JSON_FILE);
            var config1 = Configuration.Builder(config).UpdateProcessorFactory(factory).Build();
            using (var client = new LdClient(config1))
            {
                Assert.Equal("on", client.StringVariation("flag1", user, ""));
            }
        }

        [Fact]
        public void SimplifiedFlagEvaluatesAsExpected()
        {
            factory.WithFilePaths(ALL_DATA_JSON_FILE);
            var config1 = Configuration.Builder(config).UpdateProcessorFactory(factory).Build();
            using (var client = new LdClient(config1))
            {
                Assert.Equal("value2", client.StringVariation("flag2", user, ""));
            }
        }
        
        private int CountFlagsInStore()
        {
            return store.All(VersionedDataKind.Features).Count;
        }

        private int CountSegmentsInStore()
        {
            return store.All(VersionedDataKind.Segments).Count;
        }

        private bool WaitForCondition(TimeSpan maxTime, Func<bool> test)
        {
            DateTime deadline = DateTime.Now.Add(maxTime);
            while (DateTime.Now < deadline)
            {
                if (test())
                {
                    return true;
                }
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }
            return false;
        }
    }
}
