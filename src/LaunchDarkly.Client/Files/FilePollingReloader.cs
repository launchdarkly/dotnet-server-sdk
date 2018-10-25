#if NETSTANDARD2_0 || NET45
#else
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace LaunchDarkly.Client.Files
{
    // An unsophisticated implementation of file monitoring that we use when there's no other
    // mechanism available.
    class FilePollingReloader : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FilePollingReloader));
        private readonly List<string> _paths;
        private readonly IDictionary<string, DateTime?> _fileTimes;
        private readonly Action _reload;
        private readonly TimeSpan _pollInterval;
        private readonly CancellationTokenSource _canceller;

        public FilePollingReloader(List<string> paths, Action reload, TimeSpan pollInterval)
        {
            _paths = paths;
            _reload = reload;
            _pollInterval = pollInterval;
            _canceller = new CancellationTokenSource();

            _fileTimes = new Dictionary<string, DateTime?>();
            foreach (var p in paths)
            {
                try
                {
                    var time = File.GetLastWriteTime(p);
                    _fileTimes[p] = time;
                }
                catch (Exception)
                {
                    _fileTimes[p] = null;
                }
            }

            Task.Run(() => PollAsync(_canceller.Token));
        }
        
        private async Task PollAsync(CancellationToken stopToken)
        {
            while (true)
            {
                try
                {
                    CheckFileTimes();
                    await Task.Delay(_pollInterval, stopToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Log.Error("Unexpected exception during file polling: " + e);
                }
            }
        }

        private void CheckFileTimes()
        {
            bool changed = false;
            foreach (var p in _paths)
            {
                try
                {
                    var time = File.GetLastWriteTime(p);
                    if (!_fileTimes[p].HasValue || _fileTimes[p].Value != time)
                    {
                        _fileTimes[p] = time;
                        changed = true;
                    }
                }
                catch (Exception)
                {
                    // We don't want to treat a missing file as a change.
                    _fileTimes[p] = null;
                }
            }
            if (changed)
            {
                _reload();
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _canceller.Cancel();
            }
        }
    }
}

#endif