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
        private readonly FlagFileDataMerger _dataMerger;
        private readonly IFileReader _fileReader;
        private readonly bool _skipMissingPaths;
        private volatile bool _started;
        private volatile bool _loadedValidData;

        public FileDataSource(IFeatureStore featureStore, List<string> paths, bool autoUpdate, TimeSpan pollInterval,
            Func<string, object> alternateParser, bool skipMissingPaths, DuplicateKeysHandling duplicateKeysHandling,
            IFileReader fileReader)
        {
            _featureStore = featureStore;
            _paths = new List<string>(paths);
            _parser = new FlagFileParser(alternateParser);
            _dataMerger = new FlagFileDataMerger(duplicateKeysHandling);
            _fileReader = fileReader;
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
                    var content = _fileReader.ReadAllText(path);
                    var data = _parser.Parse(content);
                    _dataMerger.AddToData(data, allData);
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

        private void TriggerReload()
        {
            if (_started)
            {
                LoadAll();
            }
        }
    }
}
