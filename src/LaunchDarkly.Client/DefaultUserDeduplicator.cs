using System;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    internal class DefaultUserDeduplicator : IUserDeduplicator
    {
        private readonly LRUCacheSet<string> _userKeys;
        private readonly TimeSpan _flushInterval;

        internal DefaultUserDeduplicator(Configuration config)
        {
            _userKeys = new LRUCacheSet<string>(config.UserKeysCapacity);
            _flushInterval = config.UserKeysFlushInterval;
        }

        TimeSpan? IUserDeduplicator.FlushInterval
        {
            get
            {
                return _flushInterval;
            }
        }

        bool IUserDeduplicator.ProcessUser(User user)
        {
            if (user == null || user.Key == null)
            {
                return false;
            }
            return !_userKeys.Add(user.Key);
        }

        void IUserDeduplicator.Flush()
        {
            _userKeys.Clear();
        }
    }
}
