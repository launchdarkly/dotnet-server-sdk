
namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Types that are used in configuring <see cref="FileDataSourceBuilder"/>.
    /// </summary>
    public static class FileDataTypes
    {
        /// <summary>
        /// Determines how duplicate feature flag or segment keys are handled.
        /// </summary>
        /// <see cref="FileDataSourceBuilder.DuplicateKeysHandling"/>
        public enum DuplicateKeysHandling
        {
            /// <summary>
            /// An exception will be thrown if keys are duplicated across files.
            /// </summary>
            Throw,

            /// <summary>
            /// Keys that are duplicated across files will be ignored, and the first occurrence will be used.
            /// </summary>
            Ignore
        }

        /// <summary>
        /// Interface for customizing how data files are read.
        /// </summary>
        /// <see cref="FileDataSourceBuilder.FileReader(IFileReader)"/>
        public interface IFileReader
        {
            /// <summary>
            /// Opens a text file, reads all lines of the file, and then closes the file.
            /// </summary>
            /// <param name="path">The file to open for reading.</param>
            /// <returns>A string containing all lines of the file.</returns>
            /// <exception cref="System.IO.FileNotFoundException">The file specified in path was not found.</exception>
            string ReadAllText(string path);
        }
    }
}
