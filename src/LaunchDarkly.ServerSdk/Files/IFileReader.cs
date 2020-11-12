namespace LaunchDarkly.Sdk.Server.Files
{
    /// <summary>
    /// Interface for customizing <see cref="FileDataSourceFactory"/>.
    /// </summary>
    /// <see cref="FileDataSourceFactory.WithFileReader(IFileReader)"/>
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