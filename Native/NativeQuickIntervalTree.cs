using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

namespace vertoker.UnityIntervalTree.Native
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
        internal NativeList<Interval<TKey, TValue>> Intervals;
        internal NativeList<IntervalHalf> IntervalsDescending;
        internal NativeList<Node> Nodes;
        internal int TreeHeight;
        internal bool IsBuilt;

        /// <inheritdoc cref="NativeQuickIntervalTree{TKey, TValue}"/>
        public NativeQuickIntervalTree(Allocator allocator) : this(32, allocator) { }

        /// <inheritdoc cref="NativeQuickIntervalTree{TKey, TValue}"/>
        public NativeQuickIntervalTree(int initialCapacity, Allocator allocator)
        {
            Intervals = new NativeList<Interval<TKey, TValue>>(initialCapacity, allocator);
            
            IntervalsDescending = new NativeList<IntervalHalf>(allocator);
            Nodes = new NativeList<Node>(allocator);
            
            TreeHeight = 0;
            IsBuilt = false;
        }

        public int Count => Intervals.Length;

        public IEnumerable<TValue> Values
        {
            get
            {
                using var enumerator = Intervals.GetEnumerator();
                while (enumerator.MoveNext())
                    yield return enumerator.Current.Value;
            }
        }

        public IEnumerator<Interval<TKey, TValue>> GetEnumerator() => Intervals.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(TKey from, TKey to, TValue value)
        {
            Intervals.Add(new Interval<TKey, TValue>(from, to, value));
            IsBuilt = false;
        }

        public IEnumerable<TValue> Query(TKey target)
        {
            return Query(target, target);
        }

        public IEnumerable<TValue> Query(TKey low, TKey high)
        {
            if (high.CompareTo(low) < 0)
                throw new ArgumentException("Argument 'high' must not be smaller than argument 'low'", nameof(high));

            if (!IsBuilt) Build();

            List<TValue> result = null;

            Span<int> stack = stackalloc int[TreeHeight];
            stack[0] = 1;
            var stackIndex = 0;

            while (stackIndex >= 0)
            {
                var nodeIndex = stack[stackIndex--];

                var node = Nodes[nodeIndex];

                if (node.IntervalCount == 0) continue;

                var compareLow = low.CompareTo(node.Center);
                var compareHigh = high.CompareTo(node.Center);

                if (compareHigh < 0)
                {
                    // look left
                    // test node intervals for overlap
                    for (var i = node.IntervalIndex; i < node.IntervalIndex + node.IntervalCount; i++)
                    {
                        var intv = Intervals[i];
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
                        var half = IntervalsDescending[i];
                        if (low.CompareTo(half.Start) <= 0)
                        {
                            var full = Intervals[half.Index];
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
                        var intv = Intervals[i];
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
        
        public void Build()
        {
            if (IsBuilt) return;

            // reset tree
            Nodes.Clear();

            // Add 'null' node and root
            Nodes.Add(new Node()); // 0-index: 'null'
            Nodes.Add(new Node()); // 1-index: root

            // ensure descending intervals array is large enough, but not too large
            if (IntervalsDescending.Length < Intervals.Length ||
                IntervalsDescending.Length > 2 * Intervals.Length)
                IntervalsDescending.Resize(Intervals.Length, NativeArrayOptions.UninitializedMemory);

            Intervals.Sort();

            BuildRec(0, Intervals.Length - 1, 1, 0);

            TreeHeight = Intervals.Length <= 1
                ? 1
                : (int)Math.Log(Intervals.Length, 2) + 1;

            IsBuilt = true;
        }
        
        void BuildRec(int min, int max, int nodeIndex, int recursionLevel)
        {
            if (recursionLevel++ > 100)
                throw new InvalidOperationException($"Excessive recursion detected, aborting to prevent stack overflow. Please check thread safety.");

            var sliceWidth = max - min + 1;

            if (sliceWidth <= 0) return;

            var centerIndex = min + sliceWidth / 2;

            // Pick Center value
            var centerValue = Intervals[centerIndex].From;

            // Move index if multiple intervals share same 'From' value
            while (centerIndex < max
                   && centerValue.CompareTo(Intervals[centerIndex + 1].From) == 0)
            {
                centerIndex++;
            }

            // Iterate through intervals and pick the ones that overlap
            var i = min;
            var nodeIntervalCount = 0;
            for (; i <= max; i++)
            {
                var interval = Intervals[i];

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
                        var tmp = Intervals[i - nodeIntervalCount];
                        Intervals[i - nodeIntervalCount] = interval;
                        Intervals[i] = tmp;
                    }
                }
            }

            var nodeIntervalIndex = i - nodeIntervalCount;

            // re-sort 'center' intervals
            // Array.Sort(_intervals, nodeIntervalIndex, nodeIntervalCount);
            var intervalSlice = new NativeSlice<Interval<TKey, TValue>>(Intervals.AsArray(), nodeIntervalIndex, nodeIntervalCount);
            intervalSlice.Sort();

            // add descending interval halves

            for (var j = nodeIntervalIndex; j < nodeIntervalIndex + nodeIntervalCount; j++)
            {
                var interval = Intervals[j];
                IntervalsDescending[j] = new IntervalHalf(interval.To, j);
            }

            // sort descending interval halves
            // Array.Sort(_intervalsDescending, nodeIntervalIndex, nodeIntervalCount);
            // Array.Reverse(_intervalsDescending, nodeIntervalIndex, nodeIntervalCount);
            var intervalsDescendingSlice = new NativeSlice<IntervalHalf>(IntervalsDescending.AsArray(), nodeIntervalIndex, nodeIntervalCount);
            intervalsDescendingSlice.Sort();
            intervalsDescendingSlice.Reverse();

            if (nodeIntervalCount == sliceWidth)
            {
                // all intervals stored, no need to recurse further
                Nodes[nodeIndex] = new Node(
                    centerValue,
                    next: 0,
                    nodeIntervalIndex,
                    nodeIntervalCount);
                return;
            }

            var nextIndex = Nodes.Length;

            // add node
            Nodes[nodeIndex] = new Node(
                centerValue,
                nextIndex,
                nodeIntervalIndex,
                nodeIntervalCount);

            // add two placeholder nodes to fixate the child indexes
            Nodes.Add(new Node());
            Nodes.Add(new Node());
            
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
            while (i < Intervals.Length)
            {
                var interval = Intervals[i];
                if (predicate(interval, state))
                {
                    Intervals.RemoveAtSwapBack(i);
                    IsBuilt = false;
                }
                else
                {
                    i++;
                }
            }
        }

        public void Clear()
        {
            Intervals.Clear();
            IsBuilt = false;
        }

        internal readonly struct Node
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

        internal readonly struct IntervalHalf : IComparable<IntervalHalf>
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
            Intervals.Dispose();
            IntervalsDescending.Dispose();
            Nodes.Dispose();
        }
    }
}
