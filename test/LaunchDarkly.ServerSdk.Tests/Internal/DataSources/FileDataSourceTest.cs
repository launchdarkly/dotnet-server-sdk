using System;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.TestHelpers;
using YamlDotNet.Serialization;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using static LaunchDarkly.Sdk.Server.TestUtils;
using static LaunchDarkly.TestHelpers.JsonAssertions;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class FileDataSourceTest : BaseTest
    {
        private static readonly string ALL_DATA_JSON_FILE = TestUtils.TestFilePath("all-properties.json");
        private static readonly string ALL_DATA_YAML_FILE = TestUtils.TestFilePath("all-properties.yml");

        private readonly CapturingDataSourceUpdates _updateSink = new CapturingDataSourceUpdates();
        private readonly FileDataSourceBuilder factory = FileData.DataSource();
        private readonly User user = User.WithKey("key");

        public FileDataSourceTest(ITestOutputHelper testOutput) : base(testOutput) { }

        private IDataSource MakeDataSource() =>
            factory.CreateDataSource(
                BasicContext,
                _updateSink);

        [Fact]
        public void FlagsAreNotLoadedUntilStart()
        {
            factory.FilePaths(ALL_DATA_JSON_FILE);
            using (var fp = MakeDataSource())
            {
                _updateSink.Inits.ExpectNoValue();
            }
        }

        [Fact]
        public void FlagsAreLoadedOnStart()
        {
            factory.FilePaths(ALL_DATA_JSON_FILE);
            using (var fp = MakeDataSource())
            {
                fp.Start();
                var initData = _updateSink.Inits.ExpectValue();
                AssertJsonEqual(DataSetAsJson(ExpectedDataSetForFullDataFile(1)), DataSetAsJson(initData));
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
                var initData = _updateSink.Inits.ExpectValue();
                AssertJsonEqual(DataSetAsJson(ExpectedDataSetForFullDataFile(1)), DataSetAsJson(initData));
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
                var initData = _updateSink.Inits.ExpectValue();
                AssertJsonEqual(DataSetAsJson(ExpectedDataSetForFullDataFile(1)), DataSetAsJson(initData));
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
                    var initData = _updateSink.Inits.ExpectValue();

                    file.SetContentFromPath(TestUtils.TestFilePath("segment-only.json"));
                    _updateSink.Inits.ExpectNoValue();
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
                    var initData = _updateSink.Inits.ExpectValue();
                    AssertJsonEqual(DataSetAsJson(ExpectedDataSetForFlagOnlyFile(1)), DataSetAsJson(initData));

                    file.SetContentFromPath(TestUtils.TestFilePath("segment-only.json"));

                    var newData = _updateSink.Inits.ExpectValue(TimeSpan.FromSeconds(5));

                    AssertJsonEqual(DataSetAsJson(ExpectedDataSetForSegmentOnlyFile(2)), DataSetAsJson(newData));
                }
            }
        }

        [Fact]
        public void FlagChangeEventIsGeneratedWhenModifiedFileIsReloaded()
        {
            using (var file = TempFile.Create())
            {
                file.SetContent(@"{""flagValues"":{""flag1"":""a""}}");

                var config = BasicConfig()
                    .DataSource(FileData.DataSource().FilePaths(file.Path).AutoUpdate(true))
                    .Build();

                using (var client = new LdClient(config))
                {
                    var events = new EventSink<FlagChangeEvent>();
                    client.FlagTracker.FlagChanged += events.Add;

                    file.SetContent(@"{""flagValues"":{""flag1"":""b""}}");

                    var e = events.ExpectValue(TimeSpan.FromSeconds(5));
                    Assert.Equal("flag1", e.Key);
                    Assert.Equal("b", client.StringVariation("flag1", user, ""));
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
                        var initData = _updateSink.Inits.ExpectValue();
                        AssertJsonEqual(DataSetAsJson(ExpectedDataSetForFlagOnlyFile(1)), DataSetAsJson(initData));

                        file2.Delete();
                        file1.SetContentFromPath(TestUtils.TestFilePath("segment-only.json"));

                        _updateSink.Inits.ExpectNoValue();
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
                    var initData = _updateSink.Inits.ExpectValue();
                    AssertJsonEqual(DataSetAsJson(ExpectedDataSetForFlagOnlyFile(1)), DataSetAsJson(initData));

                    file1.SetContentFromPath(TestUtils.TestFilePath("segment-only.json"));

                    var newData = _updateSink.Inits.ExpectValue(TimeSpan.FromSeconds(5));

                    AssertJsonEqual(DataSetAsJson(ExpectedDataSetForSegmentOnlyFile(2)), DataSetAsJson(newData));
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
                    _updateSink.Inits.ExpectNoValue();

                    file.SetContentFromPath(TestUtils.TestFilePath("segment-only.json"));

                    var newData = _updateSink.Inits.ExpectValue(TimeSpan.FromSeconds(5));

                    AssertJsonEqual(DataSetAsJson(ExpectedDataSetForSegmentOnlyFile(2)), DataSetAsJson(newData));
                    // Note that the expected version is 2 because we increment the version on each
                    // *attempt* to load the files, not on each successful load.
                }
            }
        }

        [Fact]
        public void FullFlagDefinitionEvaluatesAsExpected()
        {
            factory.FilePaths(ALL_DATA_JSON_FILE);
            var config1 = BasicConfig().DataSource(factory).Build();
            using (var client = new LdClient(config1))
            {
                Assert.Equal("on", client.StringVariation("flag1", user, ""));
            }
        }

        [Fact]
        public void SimplifiedFlagEvaluatesAsExpected()
        {
            factory.FilePaths(ALL_DATA_JSON_FILE);
            var config1 = BasicConfig().DataSource(factory).Build();
            using (var client = new LdClient(config1))
            {
                Assert.Equal("value2", client.StringVariation("flag2", user, ""));
            }
        }

        private static FullDataSet<ItemDescriptor> ExpectedDataSetForFullDataFile(int version) =>
            new DataSetBuilder()
                .Flags(
                    new FeatureFlagBuilder("flag1").Version(version).On(true).FallthroughVariation(2)
                        .Variations(LdValue.Of("fall"), LdValue.Of("off"), LdValue.Of("on")).Build(),
                    new FeatureFlagBuilder("flag2").Version(version).On(true).FallthroughVariation(0)
                        .Variations(LdValue.Of("value2")).Build()
                )
                .Segments(
                    new SegmentBuilder("seg1").Version(version).Included("user1").Build()
                )
                .Build();

        private static FullDataSet<ItemDescriptor> ExpectedDataSetForFlagOnlyFile(int version) =>
            new DataSetBuilder()
                .Flags(
                    new FeatureFlagBuilder("flag1").Version(version).On(true).FallthroughVariation(2)
                        .Variations(LdValue.Of("fall"), LdValue.Of("off"), LdValue.Of("on")).Build()
                )
                .Segments()
                .Build();

        private static FullDataSet<ItemDescriptor> ExpectedDataSetForSegmentOnlyFile(int version) =>
            new DataSetBuilder()
                .Flags()
                .Segments(
                    new SegmentBuilder("seg1").Version(version).Included("user1").Build()
                )
                .Build();
    }
}
