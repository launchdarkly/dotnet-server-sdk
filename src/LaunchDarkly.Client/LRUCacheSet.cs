using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.Client
{
    // Simple, non-thread-safe LRU cache implementation used by EventSummarizer.
    internal class LRUCacheSet<A>
    {
        private int _capacity;
        private Dictionary<A, LinkedListNode<A>> _map = new Dictionary<A, LinkedListNode<A>>();
        private LinkedList<A> _lruList = new LinkedList<A>();

        public LRUCacheSet(int capacity)
        {
            _capacity = capacity;
        }

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

        public void Clear()
        {
            _map.Clear();
            _lruList.Clear();
        }
    }
}
