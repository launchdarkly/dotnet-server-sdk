using System;
using System.Collections.Generic;
using System.IO;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    /// <summary>
    /// Implementation of file monitoring using FileSystemWatcher.
    /// </summary>
    internal sealed class FileWatchingReloader : IDisposable
    {
        private readonly ISet<string> _filePaths;
        private readonly Action _reload;
        private readonly List<FileSystemWatcher> _watchers;
        
        public FileWatchingReloader(List<string> paths, Action reload)
        {
            _reload = reload;
        
            _filePaths = new HashSet<string>();
            var dirPaths = new HashSet<string>();
            foreach (var p in paths)
            {
                var absPath = Path.GetFullPath(p);
                _filePaths.Add(absPath);
                var dirPath = Path.GetDirectoryName(absPath);
                dirPaths.Add(dirPath);
            }

            _watchers = new List<FileSystemWatcher>();
            foreach (var dir in dirPaths)
            {
                var w = new FileSystemWatcher(dir);

                w.Changed += (s, args) => ChangedPath(args.FullPath);
                w.Created += (s, args) => ChangedPath(args.FullPath);
                w.Renamed += (s, args) => ChangedPath(args.FullPath);
                w.EnableRaisingEvents = true;

                _watchers.Add(w);
            }
        }
        
        private void ChangedPath(string path)
        {
            if (_filePaths.Contains(path))
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
                foreach (var w in _watchers)
                {
                    w.Dispose();
                }
            }
        }
    }
}
