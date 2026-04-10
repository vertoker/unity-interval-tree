using System;
using Unity.Collections;

namespace vertoker.UnityIntervalTree.Native
{
    public static class NativeTreeExtensions
    {
        public static void Query<TKey, TValue>(this NativeQuickIntervalTree<TKey, TValue> tree,
            TKey target, ref NativeList<TValue> result)
            where TKey : unmanaged, IComparable<TKey> where TValue : unmanaged
        {
            tree.Query(target, target, ref result);
        }
        
        public static void Query<TKey, TValue>(this NativeQuickIntervalTree<TKey, TValue> tree, 
            TKey low, TKey high, ref NativeList<TValue> result) 
            where TKey : unmanaged, IComparable<TKey> where TValue : unmanaged
        {
            if (high.CompareTo(low) < 0)
                throw new ArgumentException("Argument 'high' must not be smaller than argument 'low'", nameof(high));

            if (!tree.IsBuilt) tree.Build();

            result.Clear();
            Span<int> stack = stackalloc int[tree.TreeHeight];
            stack[0] = 1;
            var stackIndex = 0;

            while (stackIndex >= 0)
            {
                var nodeIndex = stack[stackIndex--];

                var node = tree.Nodes[nodeIndex];

                if (node.IntervalCount == 0) continue;

                var compareLow = low.CompareTo(node.Center);
                var compareHigh = high.CompareTo(node.Center);

                if (compareHigh < 0)
                {
                    // look left
                    // test node intervals for overlap
                    for (var i = node.IntervalIndex; i < node.IntervalIndex + node.IntervalCount; i++)
                    {
                        var intv = tree.Intervals[i];
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
                        var half = tree.IntervalsDescending[i];
                        if (low.CompareTo(half.Start) <= 0)
                        {
                            var full = tree.Intervals[half.Index];
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
                        var intv = tree.Intervals[i];
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
        
        public static void Query<TKey, TValue>(this NativeQuickIntervalTree<TKey, TValue> tree,
            TKey target, ref NativeList<ValueIndexed<TValue>> result)
            where TKey : unmanaged, IComparable<TKey> where TValue : unmanaged
        {
            tree.Query(target, target, ref result);
        }
        
        public static void Query<TKey, TValue>(this NativeQuickIntervalTree<TKey, TValue> tree, 
            TKey low, TKey high, ref NativeList<ValueIndexed<TValue>> result) 
            where TKey : unmanaged, IComparable<TKey> where TValue : unmanaged
        {
            if (high.CompareTo(low) < 0)
                throw new ArgumentException("Argument 'high' must not be smaller than argument 'low'", nameof(high));

            if (!tree.IsBuilt) tree.Build();

            result.Clear();
            Span<int> stack = stackalloc int[tree.TreeHeight];
            stack[0] = 1;
            var stackIndex = 0;

            while (stackIndex >= 0)
            {
                var nodeIndex = stack[stackIndex--];

                var node = tree.Nodes[nodeIndex];

                if (node.IntervalCount == 0) continue;

                var compareLow = low.CompareTo(node.Center);
                var compareHigh = high.CompareTo(node.Center);

                if (compareHigh < 0)
                {
                    // look left
                    // test node intervals for overlap
                    for (var i = node.IntervalIndex; i < node.IntervalIndex + node.IntervalCount; i++)
                    {
                        var intv = tree.Intervals[i];
                        if (high.CompareTo(intv.From) >= 0)
                        {
                            result.Add(new ValueIndexed<TValue>(result.Length, intv.Value));
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
                        var half = tree.IntervalsDescending[i];
                        if (low.CompareTo(half.Start) <= 0)
                        {
                            var full = tree.Intervals[half.Index];
                            result.Add(new ValueIndexed<TValue>(result.Length, full.Value));
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
                        var intv = tree.Intervals[i];
                        result.Add(new ValueIndexed<TValue>(result.Length, intv.Value));
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
    }
}