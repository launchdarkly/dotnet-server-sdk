using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal sealed class FileDataSource : IDataSource
    {
        private readonly IDataSourceUpdates _dataSourceUpdates;
        private readonly List<string> _paths;
        private readonly IDisposable _reloader;
        private readonly FlagFileParser _parser;
        private readonly FlagFileDataMerger _dataMerger;
        private readonly FileDataTypes.IFileReader _fileReader;
        private readonly bool _skipMissingPaths;
        private readonly Logger _logger;
        private volatile bool _started;
        private volatile bool _loadedValidData;

        public FileDataSource(IDataSourceUpdates dataSourceUpdates, FileDataTypes.IFileReader fileReader,
            List<string> paths, bool autoUpdate, Func<string, object> alternateParser, bool skipMissingPaths,
            FileDataTypes.DuplicateKeysHandling duplicateKeysHandling,
            Logger logger)
        {
            _logger = logger;
            _dataSourceUpdates = dataSourceUpdates;
            _paths = new List<string>(paths);
            _parser = new FlagFileParser(alternateParser);
            _dataMerger = new FlagFileDataMerger(duplicateKeysHandling);
            _fileReader = fileReader;
            _skipMissingPaths = skipMissingPaths;
            if (autoUpdate)
            {
                try
                {
                    _reloader = new FileWatchingReloader(_paths, TriggerReload);
                }
                catch (Exception e)
                {
                    LogHelpers.LogException(_logger, "Unable to watch files for auto-updating", e);
                    _reloader = null;
                }
            }
            else
            {
                _reloader = null;
            }
        }

        public Task<bool> Start()
        {
            _started = true;
            LoadAll();

            // We always complete the start task regardless of whether we successfully loaded data or not;
            // if the data files were bad, they're unlikely to become good within the short interval that
            // LdClient waits on this task, even if auto-updating is on.
            TaskCompletionSource<bool> initTask = new TaskCompletionSource<bool>();
            initTask.SetResult(_loadedValidData);
            return initTask.Task;
        }

        public bool Initialized()
        {
            return _loadedValidData;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reloader?.Dispose();
            }
        }

        private void LoadAll()
        {
            var flags = new Dictionary<string, ItemDescriptor>();
            var segments = new Dictionary<string, ItemDescriptor>();
            foreach (var path in _paths)
            {
                try
                {
                    var content = _fileReader.ReadAllText(path);
                    var data = _parser.Parse(content);
                    _dataMerger.AddToData(data, flags, segments);
                }
                catch (FileNotFoundException) when (_skipMissingPaths)
                {
                    _logger.Debug("{0}: {1}", path, "File not found");
                }
                catch (Exception e)
                {
                    LogHelpers.LogException(_logger, "Failed to load " + path, e);
                    return;
                }
            }
            var allData = new FullDataSet<ItemDescriptor>(
                ImmutableDictionary.Create<DataKind, KeyedItems<ItemDescriptor>>()
                    .SetItem(DataModel.Features, new KeyedItems<ItemDescriptor>(flags))
                    .SetItem(DataModel.Segments, new KeyedItems<ItemDescriptor>(segments))
            );
            _dataSourceUpdates.Init(allData);
            _loadedValidData = true;
        }

        private const int ReadFileRetryDelay = 200;
        private const int ReadFileRetryAttempts = 30000 / ReadFileRetryDelay;

        private static string ReadFileContent(string path)
        {
            int delay = 0;
            for (int i = 0; ; i++)
            {
                try
                {
                    string content = File.ReadAllText(path);
                    return content;
                }
                catch (IOException e) when (IsFileLocked(e))
                {
                    // Retry for approximately 30 seconds before throwing
                    if (i > ReadFileRetryAttempts)
                    {
                        throw;
                    }
                    Thread.Sleep(delay);
                    // Retry immediately the first time but 200ms thereafter
                    delay = ReadFileRetryDelay;
                }
            }
        }

        private static bool IsFileLocked(IOException exception)
        {
            // We cannot guarantee that these HResult values will be present on non-Windows OSes. However, this
            // logic is less important on other platforms, because in Unix-like OSes you can atomically replace a
            // file's contents (by creating a temporary file and then renaming it to overwrite the original file),
            // so FileDataSource will not try to read an incomplete update; that is not possibble in Windows.
            int errorCode = exception.HResult & 0xffff;
            switch (errorCode)
            {
                case 0x20: // ERROR_SHARING_VIOLATION
                case 0x21: // ERROR_LOCK_VIOLATION
                    return true;
                default:
                    return false;
            }
        }

        private void TriggerReload()
        {
            if (_started)
            {
                LoadAll();
            }
        }
    }
}
