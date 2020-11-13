using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.DataSources;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class FileDataSourceBuilderTest
    {
        [Fact]
        public void AutoUpdate()
        {
            Assert.False(FileData.DataSource()._autoUpdate);

            Assert.True(FileData.DataSource().AutoUpdate(true)._autoUpdate);
        }

        [Fact]
        public void DuplicateKeysHandling()
        {
            Assert.Equal(FileDataTypes.DuplicateKeysHandling.Throw,
                FileData.DataSource()._duplicateKeysHandling);

            Assert.Equal(FileDataTypes.DuplicateKeysHandling.Ignore,
                FileData.DataSource().DuplicateKeysHandling(FileDataTypes.DuplicateKeysHandling.Ignore)
                    ._duplicateKeysHandling);
        }

        [Fact]
        public void FilePaths()
        {
            Assert.Empty(FileData.DataSource()._paths);

            var fd = FileData.DataSource();
            fd.FilePaths("path1");
            fd.FilePaths("path2", "path3");
            Assert.Equal(new List<string> { "path1", "path2", "path3" }, fd._paths);
        }

        [Fact]
        public void FileReader()
        {
            Assert.Same(FlagFileReader.Instance, FileData.DataSource()._fileReader);

            var other = new TestFileReader();
            Assert.Same(other, FileData.DataSource().FileReader(other)._fileReader);
        }

        [Fact]
        public void Parser()
        {
            Assert.Null(FileData.DataSource()._parser);

            Func<string, object> p = s => s;
            Assert.Same(p, FileData.DataSource().Parser(p)._parser);
        }

        [Fact]
        public void SkipMissingPaths()
        {
            Assert.False(FileData.DataSource()._skipMissingPaths);

            Assert.True(FileData.DataSource().SkipMissingPaths(true)._skipMissingPaths);
        }

        private class TestFileReader : FileDataTypes.IFileReader
        {
            public string ReadAllText(string path) => null;
        }
    }
}
