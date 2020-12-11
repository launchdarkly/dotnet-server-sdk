using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataSources;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// A builder for configuring the file data source.
    /// </summary>
    /// <remarks>
    /// To use the file data source, obtain a new instance of this class with <see cref="FileData.DataSource"/>;
    /// call the builder method {@link #filePaths(String...)} to specify file path(s), and/or
    /// {@link #classpathResources(String...)} to specify classpath data resources; then pass the resulting
    /// object to <see cref="ConfigurationBuilder.DataSource(Interfaces.IDataSourceFactory)"/>.
    /// </remarks>
    /// <seealso cref="FileData"/>
    public sealed class FileDataSourceBuilder : IDataSourceFactory
    {
        internal readonly List<string> _paths = new List<string>();
        internal bool _autoUpdate = false;
        internal FileDataTypes.IFileReader _fileReader = FlagFileReader.Instance;
        internal Func<string, object> _parser = null;
        internal bool _skipMissingPaths = false;
        internal FileDataTypes.DuplicateKeysHandling _duplicateKeysHandling = FileDataTypes.DuplicateKeysHandling.Throw;

        /// <summary>
        /// Adds any number of source files for loading flag data, specifying each file path as a string.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The files will not actually be loaded until the LaunchDarkly client starts up.
        /// </para>
        /// <para>
        /// Files are normally expected to contain JSON; see <see cref="Parser(Func{string, object})"/> for alternatives.
        /// </para>
        /// </remarks>
        /// <param name="paths">path(s) to the source file(s); may be absolute or relative to the current working directory</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceBuilder FilePaths(params string[] paths)
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
        /// the LaunchDarkly SDK does not import a YAML parser, but you can use <c>Parser</c> to specify a parser of
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
        ///     var source = FileData.DataSource()
        ///         .FilePaths(myYamlFilePath)
        ///         .Parser(s => yaml.Deserialize&lt;object&gt;(s));
        /// </code>
        /// </remarks>
        /// <param name="parseFn">the parsing function</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceBuilder Parser(Func<string, object> parseFn)
        {
            _parser = parseFn;
            return this;
        }

        /// <summary>
        /// Specifies an alternate file reader which can support custom OS error handling and retry logic.
        /// </summary>
        /// <param name="fileReader">The flag file reader.</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceBuilder FileReader(FileDataTypes.IFileReader fileReader)
        {
            _fileReader = fileReader;
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
        public FileDataSourceBuilder AutoUpdate(bool autoUpdate)
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
        public FileDataSourceBuilder DuplicateKeysHandling(FileDataTypes.DuplicateKeysHandling duplicateKeysHandling)
        {
            _duplicateKeysHandling = duplicateKeysHandling;
            return this;
        }

        /// <summary>
        /// Specifies to ignore missing file paths instead of treating them as an error.
        /// </summary>
        /// <param name="skipMissingPaths">If <c>true</c>, missing file paths will be skipped,
        /// otherwise they will be treated as an error</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceBuilder SkipMissingPaths(bool skipMissingPaths)
        {
            _skipMissingPaths = skipMissingPaths;
            return this;
        }

        /// <inheritdoc/>
        public IDataSource CreateDataSource(LdClientContext context, IDataSourceUpdates dataSourceUpdates)
        {
            return new FileDataSource(dataSourceUpdates, _fileReader, _paths, _autoUpdate,
                _parser, _skipMissingPaths, _duplicateKeysHandling,
                context.Basic.Logger.SubLogger(LogNames.DataSourceSubLog));
        }
    }
}
