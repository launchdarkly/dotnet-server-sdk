using System.Collections.Generic;

namespace LaunchDarkly.Sdk.Server.Internal
{
    // Simple, non-thread-safe LRU cache implementation used by DefaultEventProcessor.
    internal class LRUCacheSet<A>
    {
        private int _capacity;
        private Dictionary<A, LinkedListNode<A>> _map = new Dictionary<A, LinkedListNode<A>>();
        private LinkedList<A> _lruList = new LinkedList<A>();

        public LRUCacheSet(int capacity)
        {
            _capacity = capacity;
        }

        /// <summary>
        /// Adds a value to the set and returns true if it was already there.
        /// </summary>
        /// <param name="value">a value</param>
        /// <returns>true if it was already in the set</returns>
        public bool Add(A value)
        {
            LinkedListNode<A> node;
            if (_map.TryGetValue(value, out node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return true;
            }
            while (_map.Count >= _capacity)
            {
                LinkedListNode<A> oldest = _lruList.Last;
                _map.Remove(oldest.Value);
                _lruList.Remove(oldest);
            }
            _map[value] = _lruList.AddFirst(value);
            return false;
        }

        /// <summary>
        /// Removes all values.
        /// </summary>
        public void Clear()
        {
            _map.Clear();
            _lruList.Clear();
        }
    }
}
