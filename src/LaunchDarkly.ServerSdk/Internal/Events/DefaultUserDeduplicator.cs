using System;
using LaunchDarkly.Sdk.Internal.Events;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    internal class DefaultUserDeduplicator : IUserDeduplicator
    {
        private readonly LRUCacheSet<string> _userKeys;
        private readonly TimeSpan _flushInterval;

        internal DefaultUserDeduplicator(int userKeysCapacity, TimeSpan userKeysFlushInterval)
        {
            _userKeys = new LRUCacheSet<string>(userKeysCapacity);
            _flushInterval = userKeysFlushInterval;
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
