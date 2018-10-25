using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;
using LaunchDarkly.Client;
using LaunchDarkly.Client.Files;

namespace LaunchDarkly.Tests
{
    public class FileDataSourceTest
    {
        private readonly IFeatureStore store = new InMemoryFeatureStore();
        private readonly FileDataSourceFactory factory = FileComponents.FileDataSource();
        private readonly Configuration config = Configuration.Default("sdkKey")
            .WithEventProcessorFactory(Components.NullEventProcessor);
        private readonly User user = User.WithKey("key");

        [Fact]
        public void FlagsAreNotLoadedUntilStart()
        {
            factory.WithFilePaths(TestUtils.TestFilePath("all-properties.json"));
            using (var fp = factory.CreateUpdateProcessor(config, store))
            {
                Assert.False(store.Initialized());
                Assert.Equal(0, CountFlagsInStore());
                Assert.Equal(0, CountSegmentsInStore());
            }
        }

        [Theory]
        [InlineData("all-properties.json")]
        [InlineData("all-properties.yml")]
        public void FlagsAreLoadedOnStart(string filename)
        {
            factory.WithFilePaths(TestUtils.TestFilePath(filename));
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
        public void StartTaskIsCompletedAndInitializedIsFalseAfterUnsuccessfulLoad()
        {
            factory.WithFilePaths("bad-file-path");
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

                    Thread.Sleep(TimeSpan.FromMilliseconds(100));

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

                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                    
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

        [Theory]
        [InlineData("all-properties.json")]
        [InlineData("all-properties.yml")]
        public void FullFlagDefinitionEvaluatesAsExpected(string filename)
        {
            factory.WithFilePaths(TestUtils.TestFilePath(filename));
            config.WithUpdateProcessorFactory(factory);
            using (var client = new LdClient(config))
            {
                Assert.Equal("on", client.StringVariation("flag1", user, ""));
            }
        }

        [Theory]
        [InlineData("all-properties.json")]
        [InlineData("all-properties.yml")]
        public void SimplifiedFlagEvaluatesAsExpected(string filename)
        {
            factory.WithFilePaths(TestUtils.TestFilePath(filename));
            config.WithUpdateProcessorFactory(factory);
            using (var client = new LdClient(config))
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
