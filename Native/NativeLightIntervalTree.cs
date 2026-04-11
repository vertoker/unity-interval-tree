using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

namespace vertoker.UnityIntervalTree.Native
{
    /// <summary>
    /// Light-weight interval tree implementation, based on an augmented interval tree.
    /// </summary>
    /// <typeparam name="TKey">Type used to specify the start and end of each intervals</typeparam>
    /// <typeparam name="TValue">Type of the value associated with each interval</typeparam>
    public struct NativeLightIntervalTree<TKey, TValue> : IBuildIntervalTree<TKey, TValue>
        where TKey : unmanaged, IComparable<TKey> where TValue : unmanaged
    {
        private NativeList<AugmentedInterval> _intervals;
        private int _treeHeight;
        private bool _isBuilt;
        
        /// <inheritdoc cref="NativeLightIntervalTree{TKey,TValue}"/>
        public NativeLightIntervalTree(Allocator allocator) : this(16, allocator) { }

        /// <inheritdoc cref="NativeLightIntervalTree{TKey, TValue}"/>
        public NativeLightIntervalTree(int initialCapacity, Allocator allocator)
        {
            _intervals = new NativeList<AugmentedInterval>(initialCapacity, allocator);
            _treeHeight = 0;
            _isBuilt = false;
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

        public IEnumerator<Interval<TKey, TValue>> GetEnumerator()
        {
            using var enumerator = _intervals.GetEnumerator();
            while (enumerator.MoveNext())
                yield return enumerator.Current.ToInterval();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(TKey from, TKey to, TValue value)
        {
            _intervals.Add(new AugmentedInterval(from, to, to, value));
            _isBuilt = false;
        }

        public IEnumerable<TValue> Query(TKey target)
        {
            return Query(target, target);
        }

        public IEnumerable<TValue> Query(TKey low, TKey high)
        {
            if (high.CompareTo(low) < 0)
                throw new ArgumentException("Argument 'high' must not be smaller than argument 'low'", nameof(high));

            if (!_isBuilt) Build();

            if (_intervals.Length == 0)
                return Enumerable.Empty<TValue>();

            List<TValue> results = null;

            Span<int> stack = stackalloc int[2 * _treeHeight];
            stack[0] = 0;
            stack[1] = _intervals.Length - 1;
            var stackIndex = 1;

            while (stackIndex > 0)
            {
                var max = stack[stackIndex--];
                var min = stack[stackIndex--];

                var span = max - min;
                if (span < 6) // At small subtree sizes a linear scan is faster
                {
                    for (var i = min; i <= max; i++)
                    {
                        var interval = _intervals[i];

                        var compareFrom = high.CompareTo(interval.From);
                        if (compareFrom < 0)
                            break;

                        var compareTo = low.CompareTo(interval.To);
                        if (compareTo > 0)
                            continue;

                        results ??= new List<TValue>();
                        results.Add(interval.Value);
                    }
                }
                else
                {
                    var center = (min + max + 1) / 2;
                    var interval = _intervals[center];

                    var compareMax = low.CompareTo(interval.Max);
                    if (compareMax > 0) continue; // target larger than Max, bail

                    // search left
                    stack[++stackIndex] = min;
                    stack[++stackIndex] = center - 1;

                    // check current node
                    var compareFrom = high.CompareTo(interval.From);

                    if (compareFrom < 0) continue; // target smaller than From, bail
                    else
                    {
                        var compareTo = low.CompareTo(interval.To);
                        if (compareTo <= 0)
                        {
                            results ??= new List<TValue>();
                            results.Add(interval.Value);
                        }
                    }

                    // search right
                    stack[++stackIndex] = center + 1;
                    stack[++stackIndex] = max;
                }
            }

            return results is null ? Enumerable.Empty<TValue>() : results;
        }
        
        public void Build()
        {
            if (_isBuilt) return;

            if (_intervals.Length is 0)
            {
                _treeHeight = 0;
                _isBuilt = true;
                return;
            }

            // order intervals
            _intervals.Sort();
            _treeHeight = (int)Math.Log(_intervals.Length, 2) + 1;

            UpdateMaxRec(0, _intervals.Length - 1, 0);

            _isBuilt = true;
        }

        private TKey UpdateMaxRec(int min, int max, int recursionLevel)
        {
            if (recursionLevel++ > 100)
                throw new InvalidOperationException($"Excessive recursion detected, aborting to prevent stack overflow. Please check thread safety.");

            var center = min + (max - min + 1) / 2;

            var interval = _intervals[center];

            if (max - min <= 0)
            {
                // set max to 'To'
                var interval2 = interval;
                interval2.Max = interval.To;
                _intervals[center] = interval2;
                return interval.To;
            }
            else
            {
                // find max between children and own 'To'
                var maxValue = interval.To;

                var left = UpdateMaxRec(min, center - 1, recursionLevel);
                var right = center < max ?
                    UpdateMaxRec(center + 1, max, recursionLevel) :
                    maxValue;

                if (left.CompareTo(maxValue) > 0)
                    maxValue = left;
                if (right.CompareTo(maxValue) > 0)
                    maxValue = right;

                // update max
                var interval2 = interval;
                interval2.Max = maxValue;
                _intervals[center] = interval2;
                return maxValue;
            }
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
                var interval = _intervals[i].ToInterval();
                if (predicate(interval, state))
                {
                    _intervals.RemoveAtSwapBack(i);
                    _isBuilt = false;
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
            _isBuilt = false;
        }

        private struct AugmentedInterval : IComparable<AugmentedInterval>
        {
            public AugmentedInterval(TKey from, TKey to, TKey max, TValue value)
            {
                if (from.CompareTo(to) > 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(from), $"'{nameof(from)}' must be less than or equal to '{nameof(to)}'");
                }

                From = from;
                To = to;
                Max = max;
                Value = value;
            }

            public TKey From;
            public TKey To;
            public TKey Max;
            public TValue Value;

            public int CompareTo(AugmentedInterval other)
            {
                var fromComparison = From.CompareTo(other.From);
                if (fromComparison != 0)
                    return fromComparison;
                return To.CompareTo(other.To);
            }

            public readonly Interval<TKey, TValue> ToInterval()
                => new(From, To, Value);
        }

        public void Dispose()
        {
            _intervals.Dispose();
        }
    }
}
