using System;
using System.Collections.Generic;

namespace LaunchDarkly.Client.Files
{
    /// <summary>
    /// A factory for the file data source described in <see cref="FileComponents"/>.
    /// </summary>
    /// <remarks>
    /// To use the file data source, obtain a new instance of this class with
    /// <see cref="FileComponents.FileDataSource"/>, call the builder method
    /// <see cref="WithFilePaths(string[])"/>, then pass the resulting object to
    /// <see cref="ConfigurationBuilder.UpdateProcessorFactory(IUpdateProcessorFactory)"/>.
    /// </remarks>
    public class FileDataSourceFactory : IUpdateProcessorFactory
    {
        /// <summary>
        /// The default value for <see cref="WithPollInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

        private readonly List<string> _paths = new List<string>();
        private bool _autoUpdate = false;
        private TimeSpan _pollInterval = DefaultPollInterval;
        private Func<string, object> _parser = null;
        private bool _skipMissingPaths = false;
        private DuplicateKeysHandling _duplicateKeysHandling = DuplicateKeysHandling.Throw;

        /// <summary>
        /// Adds any number of source files for loading flag data, specifying each file path as a string.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The files will not actually be loaded until the LaunchDarkly client starts up.
        /// </para>
        /// <para>
        /// Files are normally expected to contain JSON; see <see cref="WithParser(Func{string, object})"/> for alternatives.
        /// </para>
        /// </remarks>
        /// <param name="paths">path(s) to the source file(s); may be absolute or relative to the current working directory</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceFactory WithFilePaths(params string[] paths)
        {
            _paths.AddRange(paths);
            return this;
        }

        /// <summary>
        /// Specifies an alternate parsing function to use for non-JSON source files.
        /// </summary>
        /// <remarks>
        /// <para>
        /// By default, the file data source attempts to parse files as JSON objects. You may wish to use another format,
        /// such as YAML. To avoid bringing in additional dependencies that might conflict with application dependencies,
        /// the LaunchDarkly SDK does not import a YAML parser, but you can use <c>WithParser</c> to specify a parser of
        /// your choice.
        /// </para>
        /// <para>
        /// The function you provide should take a string and return an <c>object</c> which should contain only the basic
        /// types that can be represented in JSON: so, for instance, string values should be <c>string</c>, arrays should
        /// be <c>List</c>, and objects with key-value pairs should be <c>Dictionary&lt;string, object&gt;</c>. It should
        /// throw an exception if it can't parse the data.
        /// </para>
        /// <para>
        /// The file data source will still try to parse files as JSON if their first non-whitespace character is '{',
        /// but if that fails, it will use the custom parser.
        /// </para>
        /// <para>
        /// Here is an example of how you would do this with the <c>YamlDotNet</c> package:
        /// </para>
        /// <code>
        ///     var yaml = new DeserializerBuilder().Build();
        ///     var source = FileComponents.FileDataSource()
        ///         .WithFilePaths(myYamlFilePath)
        ///         .WithParser(s => yaml.Deserialize&lt;object&gt;(s));
        /// </code>
        /// </remarks>
        /// <param name="parseFn">the parsing function</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceFactory WithParser(Func<string, object> parseFn)
        {
            _parser = parseFn;
            return this;
        }

        /// <summary>
        /// Specifies whether the data source should watch for changes to the source file(s) and reload flags
        /// whenever there is a change.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This option is off by default: unless you set this option to <c>true</c>, files will only be loaded once.
        /// </para>
        /// <para>
        /// In .NET Framework, and .NET Standard 2.0, file changes are detected by <c>System.IO.FileSystemWatcher</c>.
        /// However, in .NET Standard 1.x, this is not available and so file changes are detected by polling the file
        /// modification times (at an interval configurable with <see cref="WithPollInterval(TimeSpan)"/>. Be aware that
        /// the latter mechanism may not detect changes that occur very frequently, since on some operating systems
        /// the file modification time does not have a high precision, so if you are running on .NET Standard 1.x you
        /// should avoid test scenarios where the data files are modified immediately after startup.
        /// </para>
        /// <para>
        /// Whenever possible, you should update a file's entire contents in one atomic operation; in Unix-like OSes,
        /// that can be done by creating a temporary file, writing to it, and then renaming it to replace the original
        /// file. In Windows, that is not always possible, so FileDataSource might detect an update before the file has
        /// been fully written; in that case it will retry until it succeeds.
        /// </para>
        /// <para>
        /// Note that auto-updating may not work if any of the files you specified has an invalid directory path.
        /// </para>
        /// </remarks>
        /// <param name="autoUpdate">true if flags should be reloaded whenever a source file changes</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceFactory WithAutoUpdate(bool autoUpdate)
        {
            _autoUpdate = autoUpdate;
            return this;
        }

        /// <summary>
        /// Specifies how to handle keys that are duplicated across files.
        /// </summary>
        /// <remarks>
        /// <para>
        /// By default, the file data source will throw if keys are duplicated across files.
        /// </para>
        /// </remarks>
        /// <param name="duplicateKeysHandling">how duplicate keys should be handled</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceFactory WithDuplicateKeysHandling(DuplicateKeysHandling duplicateKeysHandling)
        {
            _duplicateKeysHandling = duplicateKeysHandling;
            return this;
        }

        /// <summary>
        /// Specifies how often to poll for file changes if polling is necessary.
        /// </summary>
        /// <remarks>
        /// This setting is only used if <see cref="WithAutoUpdate(bool)"/> is true and if you are using .NET
        /// Standard 1.x; otherwise it is ignored. The default value is <see cref="DefaultPollInterval"/>.
        /// </remarks>
        /// <param name="pollInterval">the interval for file polling</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceFactory WithPollInterval(TimeSpan pollInterval)
        {
            _pollInterval = pollInterval;
            return this;
        }

        /// <summary>
        /// Specifies to ignore missing file paths instead of treating them as an error.
        /// </summary>
        /// <param name="skipMissingPaths">If <c>true</c>, missing file paths will be skipped,
        /// otherwise they will be treated as an error</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceFactory WithSkipMissingPaths(bool skipMissingPaths)
        {
            _skipMissingPaths = skipMissingPaths;
            return this;
        }

        /// <summary>
        /// Used internally by the LaunchDarkly client.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="featureStore"></param>
        /// <returns>the component instance</returns>
        public IUpdateProcessor CreateUpdateProcessor(Configuration config, IFeatureStore featureStore)
        {
            return new FileDataSource(featureStore, _paths, _autoUpdate, _pollInterval, _parser, _skipMissingPaths,
                _duplicateKeysHandling);
        }
    }
}
