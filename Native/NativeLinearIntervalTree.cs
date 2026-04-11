using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

namespace vertoker.UnityIntervalTree.Native
{
    /// <summary>
    /// Simple implementation using full scans of an unordered array, not actually a tree. O(n) queries.
    /// Does not require an expensive build process, making it the fastest options when (#intervals * #queries) &lt; 1000
    /// </summary>
    /// <typeparam name="TKey">Type used to specify the start and end of each intervals</typeparam>
    /// <typeparam name="TValue">Type of the value associated with each interval</typeparam>
    public struct NativeLinearIntervalTree<TKey, TValue> : IIntervalTree<TKey, TValue>
        where TKey : unmanaged, IComparable<TKey> where TValue : unmanaged
    {
        private NativeList<Interval<TKey, TValue>> _intervals;
        
        /// <inheritdoc cref="NativeLinearIntervalTree{TKey, TValue}"/>
        public NativeLinearIntervalTree(Allocator allocator) : this(32, allocator) { }

        /// <inheritdoc cref="NativeLinearIntervalTree{TKey, TValue}"/>
        public NativeLinearIntervalTree(int initialCapacity, Allocator allocator)
        {
            _intervals = new NativeList<Interval<TKey, TValue>>(initialCapacity, allocator);
        }
        
        public int Count => _intervals.Length;

        public IEnumerable<TValue> Values
        {
            get
            {
                using var enumerator = _intervals.GetEnumerator();
                while (enumerator.MoveNext())
                    yield return enumerator.Current.Value;
            }
        }

        public IEnumerator<Interval<TKey, TValue>> GetEnumerator() => _intervals.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(TKey from, TKey to, TValue value)
        {
            _intervals.Add(new Interval<TKey, TValue>(from, to, value));
        }

        public IEnumerable<TValue> Query(TKey target)
        {
            return Query(target, target);
        }

        public IEnumerable<TValue> Query(TKey low, TKey high)
        {
            if (high.CompareTo(low) < 0)
                throw new ArgumentException("Argument 'high' must not be smaller than argument 'low'", nameof(high));

            if (_intervals.Length == 0)
                return Enumerable.Empty<TValue>();

            List<TValue> results = null;

            foreach (var interval in _intervals)
            {
                var compareFrom = high.CompareTo(interval.From);
                if (compareFrom < 0)
                    continue;

                var compareTo = low.CompareTo(interval.To);
                if (compareTo > 0)
                    continue;

                results ??= new List<TValue>();
                results.Add(interval.Value);
            }

            return results ?? Enumerable.Empty<TValue>();
        }

        public void Remove(TValue value)
        {
            RemoveAll(
                static (interval, val) => Equals(interval.Value, val),
                value);
        }

        public void Remove(IEnumerable<TValue> values)
        {
            foreach (var val in values)
                Remove(val);
        }

        public void RemoveAll<TState>(Func<Interval<TKey, TValue>, TState, bool> predicate, TState state)
        {
            var i = 0;
            while (i < _intervals.Length)
            {
                var interval = _intervals[i];
                if (predicate(interval, state))
                {
                    _intervals.RemoveAtSwapBack(i);
                }
                else
                {
                    i++;
                }
            }
        }

        public void Clear()
        {
            _intervals.Clear();
        }

        public void Dispose()
        {
            _intervals.Dispose();
        }
    }
}
