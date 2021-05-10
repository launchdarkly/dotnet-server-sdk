using System;
using System.Linq;
using System.Threading;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using YamlDotNet.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class FileDataSourceTest : BaseTest
    {
        private const string sdkKey = "sdkKey";
        private static readonly string ALL_DATA_JSON_FILE = TestUtils.TestFilePath("all-properties.json");
        private static readonly string ALL_DATA_YAML_FILE = TestUtils.TestFilePath("all-properties.yml");

        private readonly IDataStore store = new InMemoryDataStore();
        private readonly FileDataSourceBuilder factory = FileData.DataSource();
        private readonly User user = User.WithKey("key");

        public FileDataSourceTest(ITestOutputHelper testOutput) : base(testOutput) { }

        private Configuration MakeConfig() =>
            Configuration.Builder(sdkKey)
                .Events(Components.NoEvents)
                .Logging(Components.Logging(testLogging))
                .Build();

        private IDataSource MakeDataSource() =>
            factory.CreateDataSource(
                new LdClientContext(new BasicConfiguration(sdkKey, false, testLogger), MakeConfig()),
                TestUtils.BasicDataSourceUpdates(store, testLogger));

        [Fact]
        public void FlagsAreNotLoadedUntilStart()
        {
            factory.FilePaths(ALL_DATA_JSON_FILE);
            using (var fp = MakeDataSource())
            {
                Assert.False(store.Initialized());
                Assert.Equal(0, CountFlagsInStore());
                Assert.Equal(0, CountSegmentsInStore());
            }
        }

        [Fact]
        public void FlagsAreLoadedOnStart()
        {
            factory.FilePaths(ALL_DATA_JSON_FILE);
            using (var fp = MakeDataSource())
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
            factory.FilePaths(ALL_DATA_YAML_FILE)
                .Parser(s => yaml.Deserialize<object>(s));
            using (var fp = MakeDataSource())
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
            using (var fp = MakeDataSource())
            {
                var task = fp.Start();
                Assert.True(task.IsCompleted);
                Assert.True(fp.Initialized);
            }
        }

        [Fact]
        public void StartTaskIsCompletedAndInitializedIsFalseAfterFailedLoadDueToMissingFile()
        {
            factory.FilePaths(ALL_DATA_JSON_FILE, "bad-file-path");
            using (var fp = MakeDataSource())
            {
                var task = fp.Start();
                Assert.True(task.IsCompleted);
                Assert.False(fp.Initialized);
            }
        }

        [Fact]
        public void CanIgnoreMissingFileOnStartup()
        {
            factory.FilePaths(ALL_DATA_JSON_FILE, "bad-file-path").SkipMissingPaths(true);
            using (var fp = MakeDataSource())
            {
                var task = fp.Start();
                Assert.True(task.IsCompleted);
                Assert.True(fp.Initialized);
                Assert.Equal(2, CountFlagsInStore());
            }
        }

        [Fact]
        public void StartTaskIsCompletedAndInitializedIsFalseAfterFailedLoadDueToMalformedFile()
        {
            factory.FilePaths(TestUtils.TestFilePath("bad-file.txt"));
            using (var fp = MakeDataSource())
            {
                var task = fp.Start();
                Assert.True(task.IsCompleted);
                Assert.False(fp.Initialized);
            }
        }
        
        [Fact]
        public void ModifiedFileIsNotReloadedIfAutoUpdateIsOff()
        {
            using (var file = TempFile.Create())
            {
                factory.FilePaths(file.Path);
                file.SetContentFromPath(TestUtils.TestFilePath("flag-only.json"));
                using (var fp = MakeDataSource())
                {
                    fp.Start();
                    file.SetContentFromPath(TestUtils.TestFilePath("segment-only.json"));
                    Thread.Sleep(TimeSpan.FromMilliseconds(400));
                    Assert.Equal(1, CountFlagsInStore());
                    Assert.Equal(0, CountSegmentsInStore());
                }
            }
        }

        [Fact]
        public void ModifiedFileIsReloadedIfAutoUpdateIsOn()
        {
            using (var file = TempFile.Create())
            {
                factory.FilePaths(file.Path).AutoUpdate(true);
                file.SetContentFromPath(TestUtils.TestFilePath("flag-only.json"));
                using (var fp = MakeDataSource())
                {
                    fp.Start();
                    Assert.True(store.Initialized());
                    Assert.Equal(0, CountSegmentsInStore());

                    Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                    // See FilePollingReloader for the reason behind this long sleep

                    file.SetContentFromPath(TestUtils.TestFilePath("segment-only.json"));

                    Assert.True(
                        WaitForCondition(TimeSpan.FromSeconds(5), () => CountSegmentsInStore() == 1),
                        "Did not detect file modification"
                    );
                }
            }
        }

        [Fact]
        public void ModifiedFileIsNotReloadedIfOneFileIsMissing()
        {
            using (var file1 = TempFile.Create())
            {
                using (var file2 = TempFile.Create())
                {
                    factory.FilePaths(file1.Path, file2.Path)
                        .AutoUpdate(true);
                    file1.SetContentFromPath(TestUtils.TestFilePath("flag-only.json"));
                    file2.SetContent("{}");
                    using (var fp = MakeDataSource())
                    {
                        fp.Start();
                        Assert.True(store.Initialized());
                        Assert.Equal(0, CountSegmentsInStore());

                        Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                        // See FilePollingReloader for the reason behind this long sleep
                        file2.Delete();
                        file1.SetContentFromPath(TestUtils.TestFilePath("segment-only.json"));

                        Thread.Sleep(TimeSpan.FromMilliseconds(400));
                        Assert.Equal(0, CountSegmentsInStore());
                    }
                }
            }
        }

        [Fact]
        public void ModifiedFileIsReloadedEvenIfOneFileIsMissingIfSkipMissingPathsIsSet()
        {
            using (var file1 = TempFile.Create())
            {
                var filename2 = TempFile.MakePathOfNonexistentFile();
                factory.FilePaths(file1.Path, filename2)
                    .SkipMissingPaths(true)
                    .AutoUpdate(true);
                file1.SetContentFromPath(TestUtils.TestFilePath("flag-only.json"));
                using (var fp = MakeDataSource())
                {
                    fp.Start();
                    Assert.True(store.Initialized());
                    Assert.Equal(0, CountSegmentsInStore());

                    Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                    // See FilePollingReloader for the reason behind this long sleep

                    file1.SetContentFromPath(TestUtils.TestFilePath("segment-only.json"));

                    Assert.True(
                        WaitForCondition(TimeSpan.FromSeconds(3), () => CountSegmentsInStore() == 1),
                        "Did not detect file modification"
                    );
                }
            }
        }

        [Fact]
        public void IfFlagsAreBadAtStartTimeAutoUpdateCanStillLoadGoodDataLater()
        {
            using (var file = TempFile.Create())
            {
                factory.FilePaths(file.Path).AutoUpdate(true);
                file.SetContent("{not correct}");
                using (var fp = MakeDataSource())
                {
                    fp.Start();
                    Assert.False(store.Initialized());

                    Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                    // See FilePollingReloader for the reason behind this long sleep

                    file.SetContentFromPath(TestUtils.TestFilePath("segment-only.json"));

                    Assert.True(
                        WaitForCondition(TimeSpan.FromSeconds(5), () => CountSegmentsInStore() == 1),
                        "Did not detect file modification"
                    );
                }
            }
        }

        [Fact]
        public void FullFlagDefinitionEvaluatesAsExpected()
        {
            factory.FilePaths(ALL_DATA_JSON_FILE);
            var config1 = Configuration.Builder(MakeConfig()).DataSource(factory).Build();
            using (var client = new LdClient(config1))
            {
                Assert.Equal("on", client.StringVariation("flag1", user, ""));
            }
        }

        [Fact]
        public void SimplifiedFlagEvaluatesAsExpected()
        {
            factory.FilePaths(ALL_DATA_JSON_FILE);
            var config1 = Configuration.Builder(MakeConfig()).DataSource(factory).Build();
            using (var client = new LdClient(config1))
            {
                Assert.Equal("value2", client.StringVariation("flag2", user, ""));
            }
        }
        
        private int CountFlagsInStore()
        {
            return store.GetAll(DataModel.Features).Items.Count();
        }

        private int CountSegmentsInStore()
        {
            return store.GetAll(DataModel.Segments).Items.Count();
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
