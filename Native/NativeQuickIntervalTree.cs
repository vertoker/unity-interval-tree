using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

namespace Jamarino.IntervalTree.Native
{
    /// <summary>
    /// Query-optimized interval tree. Implementation is based on a centered interval tree.
    /// Each interval is stored twice, and so the memory usage is higher than that of <seealso cref="LightIntervalTree"/>.
    /// </summary>
    /// <typeparam name="TKey">Type used to specify the start and end of each intervals</typeparam>
    /// <typeparam name="TValue">Type of the value associated with each interval</typeparam>
    public unsafe struct NativeQuickIntervalTree<TKey, TValue> : IBuildIntervalTree<TKey, TValue>
        where TKey : unmanaged, IComparable<TKey> where TValue : unmanaged
    {
        private NativeList<Interval<TKey, TValue>> _intervals;
        private NativeList<IntervalHalf> _intervalsDescending;
        private NativeList<Node> _nodes;
        private int _treeHeight;
        private bool _isBuilt;

        /// <inheritdoc cref="NativeQuickIntervalTree{TKey, TValue}"/>
        public NativeQuickIntervalTree(Allocator allocator) : this(32, allocator) { }

        /// <inheritdoc cref="NativeQuickIntervalTree{TKey, TValue}"/>
        public NativeQuickIntervalTree(int initialCapacity, Allocator allocator)
        {
            _intervals = new NativeList<Interval<TKey, TValue>>(initialCapacity, allocator);
            
            _intervalsDescending = new NativeList<IntervalHalf>(allocator);
            _nodes = new NativeList<Node>(allocator);
            
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

        public IEnumerator<Interval<TKey, TValue>> GetEnumerator() => _intervals.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(TKey from, TKey to, TValue value)
        {
            _intervals.Add(new Interval<TKey, TValue>(from, to, value));
            _isBuilt = false;
        }

        public IEnumerable<TValue> Query(TKey target)
        {
            return Query(target, target);
        }
        public void Query(TKey target, ref NativeList<TValue> result)
        {
            Query(target, target, ref result);
        }

        public IEnumerable<TValue> Query(TKey low, TKey high)
        {
            if (high.CompareTo(low) < 0)
                throw new ArgumentException("Argument 'high' must not be smaller than argument 'low'", nameof(high));

            if (!_isBuilt) Build();

            List<TValue> result = null;

            Span<int> stack = stackalloc int[_treeHeight];
            stack[0] = 1;
            var stackIndex = 0;

            while (stackIndex >= 0)
            {
                var nodeIndex = stack[stackIndex--];

                var node = _nodes[nodeIndex];

                if (node.IntervalCount == 0) continue;

                var compareLow = low.CompareTo(node.Center);
                var compareHigh = high.CompareTo(node.Center);

                if (compareHigh < 0)
                {
                    // look left
                    // test node intervals for overlap
                    for (var i = node.IntervalIndex; i < node.IntervalIndex + node.IntervalCount; i++)
                    {
                        var intv = _intervals[i];
                        if (high.CompareTo(intv.From) >= 0)
                        {
                            result ??= new List<TValue>();
                            result.Add(intv.Value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (node.Next is not 0)
                    {
                        // queue left child
                        stack[++stackIndex] = node.Next;
                    }
                }
                else if (compareLow > 0)
                {
                    // look right
                    // test node intervals for overlap
                    for (var i = node.IntervalIndex; i < node.IntervalIndex + node.IntervalCount; i++)
                    {
                        var half = _intervalsDescending[i];
                        if (low.CompareTo(half.Start) <= 0)
                        {
                            var full = _intervals[half.Index];
                            result ??= new List<TValue>();
                            result.Add(full.Value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (node.Next is not 0)
                    {
                        // queue right child
                        stack[++stackIndex] = node.Next + 1;
                    }
                }
                else
                {
                    // add all node intervals
                    for (var i = node.IntervalIndex; i < node.IntervalIndex + node.IntervalCount; i++)
                    {
                        var intv = _intervals[i];
                        result ??= new List<TValue>();
                        result.Add(intv.Value);
                    }

                    if (node.Next is not 0)
                    {
                        // queue left child
                        stack[++stackIndex] = node.Next;
                        // queue right child
                        stack[++stackIndex] = node.Next + 1;
                    }
                }
            }

            return result ?? Enumerable.Empty<TValue>();
        }
        
        public void Query(TKey low, TKey high, ref NativeList<TValue> result)
        {
            if (high.CompareTo(low) < 0)
                throw new ArgumentException("Argument 'high' must not be smaller than argument 'low'", nameof(high));

            if (!_isBuilt) Build();

            result.Clear();
            Span<int> stack = stackalloc int[_treeHeight];
            stack[0] = 1;
            var stackIndex = 0;

            while (stackIndex >= 0)
            {
                var nodeIndex = stack[stackIndex--];

                var node = _nodes[nodeIndex];

                if (node.IntervalCount == 0) continue;

                var compareLow = low.CompareTo(node.Center);
                var compareHigh = high.CompareTo(node.Center);

                if (compareHigh < 0)
                {
                    // look left
                    // test node intervals for overlap
                    for (var i = node.IntervalIndex; i < node.IntervalIndex + node.IntervalCount; i++)
                    {
                        var intv = _intervals[i];
                        if (high.CompareTo(intv.From) >= 0)
                        {
                            result.Add(intv.Value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (node.Next is not 0)
                    {
                        // queue left child
                        stack[++stackIndex] = node.Next;
                    }
                }
                else if (compareLow > 0)
                {
                    // look right
                    // test node intervals for overlap
                    for (var i = node.IntervalIndex; i < node.IntervalIndex + node.IntervalCount; i++)
                    {
                        var half = _intervalsDescending[i];
                        if (low.CompareTo(half.Start) <= 0)
                        {
                            var full = _intervals[half.Index];
                            result.Add(full.Value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (node.Next is not 0)
                    {
                        // queue right child
                        stack[++stackIndex] = node.Next + 1;
                    }
                }
                else
                {
                    // add all node intervals
                    for (var i = node.IntervalIndex; i < node.IntervalIndex + node.IntervalCount; i++)
                    {
                        var intv = _intervals[i];
                        result.Add(intv.Value);
                    }

                    if (node.Next is not 0)
                    {
                        // queue left child
                        stack[++stackIndex] = node.Next;
                        // queue right child
                        stack[++stackIndex] = node.Next + 1;
                    }
                }
            }
        }
        
        public void Build()
        {
            if (_isBuilt) return;

            // reset tree
            _nodes.Clear();

            // Add 'null' node and root
            _nodes.Add(new Node()); // 0-index: 'null'
            _nodes.Add(new Node()); // 1-index: root

            // ensure descending intervals array is large enough, but not too large
            if (_intervalsDescending.Length < _intervals.Length ||
                _intervalsDescending.Length > 2 * _intervals.Length)
                _intervalsDescending.Resize(_intervals.Length, NativeArrayOptions.UninitializedMemory);

            _intervals.Sort();

            BuildRec(0, _intervals.Length - 1, 1, 0);

            _treeHeight = _intervals.Length <= 1
                ? 1
                : (int)Math.Log(_intervals.Length, 2) + 1;

            _isBuilt = true;
        }
        
        void BuildRec(int min, int max, int nodeIndex, int recursionLevel)
        {
            if (recursionLevel++ > 100)
                throw new InvalidOperationException($"Excessive recursion detected, aborting to prevent stack overflow. Please check thread safety.");

            var sliceWidth = max - min + 1;

            if (sliceWidth <= 0) return;

            var centerIndex = min + sliceWidth / 2;

            // Pick Center value
            var centerValue = _intervals[centerIndex].From;

            // Move index if multiple intervals share same 'From' value
            while (centerIndex < max
                   && centerValue.CompareTo(_intervals[centerIndex + 1].From) == 0)
            {
                centerIndex++;
            }

            // Iterate through intervals and pick the ones that overlap
            var i = min;
            var nodeIntervalCount = 0;
            for (; i <= max; i++)
            {
                var interval = _intervals[i];

                if (interval.From.CompareTo(centerValue) > 0)
                {
                    // no more overlapping intervals, the rest fall to right side
                    break;
                }
                else if (interval.To.CompareTo(centerValue) >= 0)
                {
                    // overlapping interval, add the desending half later
                    nodeIntervalCount++;
                }
                else
                {
                    if (nodeIntervalCount > 0)
                    {
                        // swap current interval with first 'center' interval
                        // this partitions the array so that all 'left' and 'center' intervals are grouped
                        // 'left' interval ordering is maintained (ascending)
                        // no data is lost, so we can work directly on the interval array and re-build in future
                        var tmp = _intervals[i - nodeIntervalCount];
                        _intervals[i - nodeIntervalCount] = interval;
                        _intervals[i] = tmp;
                    }
                }
            }

            var nodeIntervalIndex = i - nodeIntervalCount;

            // re-sort 'center' intervals
            // Array.Sort(_intervals, nodeIntervalIndex, nodeIntervalCount);
            var intervalSlice = new NativeSlice<Interval<TKey, TValue>>(_intervals.AsArray(), nodeIntervalIndex, nodeIntervalCount);
            intervalSlice.Sort();

            // add descending interval halves

            for (var j = nodeIntervalIndex; j < nodeIntervalIndex + nodeIntervalCount; j++)
            {
                var interval = _intervals[j];
                _intervalsDescending[j] = new IntervalHalf(interval.To, j);
            }

            // sort descending interval halves
            // Array.Sort(_intervalsDescending, nodeIntervalIndex, nodeIntervalCount);
            // Array.Reverse(_intervalsDescending, nodeIntervalIndex, nodeIntervalCount);
            var intervalsDescendingSlice = new NativeSlice<IntervalHalf>(_intervalsDescending.AsArray(), nodeIntervalIndex, nodeIntervalCount);
            intervalsDescendingSlice.Sort();
            intervalsDescendingSlice.Reverse();

            if (nodeIntervalCount == sliceWidth)
            {
                // all intervals stored, no need to recurse further
                _nodes[nodeIndex] = new Node(
                    centerValue,
                    next: 0,
                    nodeIntervalIndex,
                    nodeIntervalCount);
                return;
            }

            var nextIndex = _nodes.Length;

            // add node
            _nodes[nodeIndex] = new Node(
                centerValue,
                nextIndex,
                nodeIntervalIndex,
                nodeIntervalCount);

            // add two placeholder nodes to fixate the child indexes
            _nodes.Add(new Node());
            _nodes.Add(new Node());
            
            BuildRec(min, i - nodeIntervalCount - 1, nextIndex, recursionLevel); // left
            BuildRec(i, max, nextIndex + 1, recursionLevel); // right
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

        private readonly struct Node
        {
            public Node(TKey center, int next, int intervalIndex, int intervalCount)
            {
                Center = center;
                Next = next;
                IntervalIndex = intervalIndex;
                IntervalCount = intervalCount;
            }

            public readonly TKey Center;
            public readonly int Next;
            public readonly int IntervalIndex;
            public readonly int IntervalCount;
        }

        private readonly struct IntervalHalf : IComparable<IntervalHalf>
        {
            public IntervalHalf(TKey start, int intervalIndex)
            {
                Start = start;
                Index = intervalIndex;
            }

            public readonly TKey Start;
            public readonly int Index;

            public int CompareTo(IntervalHalf other)
            {
                var cmp = Start.CompareTo(other.Start);
                if (cmp == 0)
                    return Index.CompareTo(other.Index);
                return cmp;
            }
        }

        public void Dispose()
        {
            _intervals.Dispose();
            _intervalsDescending.Dispose();
            _nodes.Dispose();
        }
    }
}
