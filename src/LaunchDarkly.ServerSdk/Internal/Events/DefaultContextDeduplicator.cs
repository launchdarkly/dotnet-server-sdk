using System;
using LaunchDarkly.Sdk.Internal.Events;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    internal class DefaultContextDeduplicator : IContextDeduplicator
    {
        private readonly LRUCacheSet<string> _contextKeys;
        private readonly TimeSpan _flushInterval;

        internal DefaultContextDeduplicator(int contextKeysCapacity, TimeSpan contextKeysFlushInterval)
        {
            _contextKeys = new LRUCacheSet<string>(contextKeysCapacity);
            _flushInterval = contextKeysFlushInterval;
        }

        public TimeSpan? FlushInterval => _flushInterval;

        public bool ProcessContext(ref Context context)
        {
            if (!context.Valid)
            {
                return false;
            }
            return !_contextKeys.Add(context.FullyQualifiedKey);
        }

        public void Flush() =>
            _contextKeys.Clear();
    }
}
