using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace LaunchDarkly.Client.Files
{
    internal class FileDataSource : IUpdateProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FileDataSource));
        private readonly IFeatureStore _featureStore;
        private readonly List<string> _paths;
        private readonly IDisposable _reloader;
        private readonly FlagFileParser _parser;
        private readonly bool _skipMissingPaths;
        private volatile bool _started;
        private volatile bool _loadedValidData;

        public FileDataSource(IFeatureStore featureStore, List<string> paths, bool autoUpdate, TimeSpan pollInterval,
            Func<string, object> alternateParser, bool skipMissingPaths)
        {
            _featureStore = featureStore;
            _paths = new List<string>(paths);
            _parser = new FlagFileParser(alternateParser);
            _skipMissingPaths = skipMissingPaths;
            if (autoUpdate)
            {
                try
                {
#if NETSTANDARD1_4 || NETSTANDARD1_6
                    _reloader = new FilePollingReloader(_paths, TriggerReload, pollInterval);
#else
                    _reloader = new FileWatchingReloader(_paths, TriggerReload);
#endif
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("Unable to watch files for auto-updating: {0}", e);
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
            Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData =
                new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>();
            foreach (var path in _paths)
            {
                try
                {
                    var content = ReadFileContent(path);
                    var data = _parser.Parse(content);
                    data.AddToData(allData);
                }
                catch (FileNotFoundException) when (_skipMissingPaths)
                {
                    Log.DebugFormat("{0}: {1}", path, "File not found");
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("{0}: {1}", path, e);
                    return;
                }
            }
            _featureStore.Init(allData);
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
#if NETSTANDARD1_4 || NETSTANDARD1_6
                    Task.Delay(delay).Wait();
#else
                    Thread.Sleep(delay);
#endif
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
