using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    public class LRUCache<TKey, TValue>
    {
        private readonly int maxSize;
        private readonly float maxAge;
        private readonly Dictionary<TKey, CacheEntry<TValue>> cache;
        private readonly Dictionary<TKey, LinkedListNode<TKey>> accessOrder;
        private readonly LinkedList<TKey> lruList;

        public LRUCache(int maxSize, float maxAge = float.MaxValue)
        {
            this.maxSize = maxSize;
            this.maxAge = maxAge;
            cache = new Dictionary<TKey, CacheEntry<TValue>>();
            accessOrder = new Dictionary<TKey, LinkedListNode<TKey>>();
            lruList = new LinkedList<TKey>();
        }

        public bool TryGet(TKey key, out TValue value)
        {
            value = default(TValue);

            if (!cache.TryGetValue(key, out CacheEntry<TValue> entry))
                return false;

            if (entry.IsExpired(maxAge))
            {
                Remove(key);
                return false;
            }

            entry.Access();
            MoveToFront(key);
            value = entry.value;
            return true;
        }

        public void Put(TKey key, TValue value)
        {
            if (cache.ContainsKey(key))
            {
                cache[key] = new CacheEntry<TValue>(value);
                MoveToFront(key);
                return;
            }

            if (cache.Count >= maxSize)
            {
                RemoveLRU();
            }

            cache[key] = new CacheEntry<TValue>(value);
            var node = lruList.AddFirst(key);
            accessOrder[key] = node;
        }

        private void MoveToFront(TKey key)
        {
            if (accessOrder.TryGetValue(key, out LinkedListNode<TKey> node))
            {
                lruList.Remove(node);
                lruList.AddFirst(node);
            }
        }

        private void RemoveLRU()
        {
            if (lruList.Count > 0)
            {
                var lastKey = lruList.Last.Value;
                Remove(lastKey);
            }
        }

        private void Remove(TKey key)
        {
            if (accessOrder.TryGetValue(key, out LinkedListNode<TKey> node))
            {
                lruList.Remove(node);
                accessOrder.Remove(key);
                cache.Remove(key);
            }
        }

        public void Clear()
        {
            cache.Clear();
            accessOrder.Clear();
            lruList.Clear();
        }

        public int Count => cache.Count;
    }
}