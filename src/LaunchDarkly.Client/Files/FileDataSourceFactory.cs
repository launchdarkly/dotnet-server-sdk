using System;
using System.Collections.Generic;

namespace LaunchDarkly.Client.Files
{
    /// <summary>
    /// To use the file data source, obtain a new instance of this class with
    /// <see cref="FileComponents.FileDataSource"/>, call the builder method
    /// <see cref="WithFilePaths(string[])"/>, then pass the resulting object to
    /// <see cref="ConfigurationExtensions.WithUpdateProcessorFactory(Configuration, IUpdateProcessorFactory)"/>.
    /// </summary>
    public class FileDataSourceFactory : IUpdateProcessorFactory
    {
        /// <summary>
        /// The default value for <see cref="WithPollInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

        private readonly List<string> _paths = new List<string>();
        private bool _autoUpdate = false;
        private TimeSpan _pollInterval = DefaultPollInterval;

        /// <summary>
        /// Adds any number of source files for loading flag data, specifying each file path as a string. The files will
        /// not actually be loaded until the LaunchDarkly client starts up.
        /// 
        /// Files will be parsed as JSON if their first non-whitespace character is '{'. Otherwise, they will be parsed as YAML.
        /// </summary>
        /// <param name="paths">path(s) to the source file(s); may be absolute or relative to the current working directory</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceFactory WithFilePaths(params string[] paths)
        {
            _paths.AddRange(paths);
            return this;
        }

        /// <summary>
        /// Specifies whether the data source should watch for changes to the source file(s) and reload flags
        /// whenever there is a change. By default, it will not, so the flags will only be loaded once.
        ///
        /// In .NET Standard 1.x, file changes are detected by polling the file modified times (at an interval
        /// configurable with <see cref="WithPollInterval"/>). In all other frameworks, file changes are
        /// detected by <c>System.IO.FileSystemWatcher</c>.
        ///
        /// Note that auto-updating may not work if any of the files you specified has an invalid directory path.
        /// </summary>
        /// <param name="autoUpdate">true if flags should be reloaded whenever a source file changes</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceFactory WithAutoUpdate(bool autoUpdate)
        {
            _autoUpdate = autoUpdate;
            return this;
        }

        /// <summary>
        /// Specifies how often to poll for file changes if polling is necessary. This setting is only used if
        /// <see cref="WithAutoUpdate(bool)"/> is true and if you are using .NET Standard 1.x; otherwise it is
        /// ignored. The default value is <see cref="DefaultPollInterval"/>.
        /// </summary>
        /// <param name="pollInterval">the interval for file polling</param>
        /// <returns>the same factory object</returns>
        public FileDataSourceFactory WithPollInterval(TimeSpan pollInterval)
        {
            _pollInterval = pollInterval;
            return this;
        }

        /// <summary>
        /// Used internally by the LaunchDarkly client.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="featureStore"></param>
        /// <returns></returns>
        public IUpdateProcessor CreateUpdateProcessor(Configuration config, IFeatureStore featureStore)
        {
            return new FileDataSource(featureStore, _paths, _autoUpdate, _pollInterval);
        }
    }
}
