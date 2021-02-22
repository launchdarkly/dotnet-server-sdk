using System;
using System.IO;
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
            var filename = Path.GetTempFileName();
            factory.FilePaths(filename);
            try
            {
                File.WriteAllText(filename, File.ReadAllText(TestUtils.TestFilePath("flag-only.json")));
                using (var fp = MakeDataSource())
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
            factory.FilePaths(filename).AutoUpdate(true);
            try
            {
                File.WriteAllText(filename, File.ReadAllText(TestUtils.TestFilePath("flag-only.json")));
                using (var fp = MakeDataSource())
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
            factory.FilePaths(filename1, filename2)
                .AutoUpdate(true);
            try
            {
                File.WriteAllText(filename1, File.ReadAllText(TestUtils.TestFilePath("flag-only.json")));
                File.WriteAllText(filename2, "{}");
                using (var fp = MakeDataSource())
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
            factory.FilePaths(filename1, filename2)
                .SkipMissingPaths(true)
                .AutoUpdate(true);
            try
            {
                File.WriteAllText(filename1, File.ReadAllText(TestUtils.TestFilePath("flag-only.json")));
                using (var fp = MakeDataSource())
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
            factory.FilePaths(filename).AutoUpdate(true);
            try
            {
                File.WriteAllText(filename, "{not correct}");
                using (var fp = MakeDataSource())
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
