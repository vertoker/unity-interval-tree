using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using vertoker.UnityIntervalTree.Native;

namespace vertoker.UnityIntervalTree.Tests.Extras
{
    public static class TreeFactory
    {
        public static readonly IEnumerable<string> TreeTypes = new string[] {
            "reference",
            "linear",
            "light",
            "quick",
            "native-linear",
            "native-light",
            "native-quick",
        };

        public static IEnumerable<string> TreeTypesSansReference = TreeTypes.Where(t => t is not "reference");

        public static IIntervalTree<TKey, TValue> CreateEmptyTree<TKey, TValue>(string type, int? capacity = null)
            where TKey : unmanaged, IComparable<TKey> where TValue : unmanaged
        {
            var tree = CreateEmptyTreeRaw<TKey, TValue>(type, capacity);

            if (type is "reference")
                return (IIntervalTree<TKey, TValue>)tree;

            return new TreeAdapter<TKey, TValue>((IIntervalTree<TKey, TValue>)tree);
        }

        public static IIntervalTree<TKey, TValue> CreateNonReferenceTree<TKey, TValue>(string type, int? capacity = null)
            where TKey : unmanaged, IComparable<TKey> where TValue : unmanaged
        {
            return (IIntervalTree<TKey, TValue>)CreateEmptyTreeRaw<TKey, TValue>(type, capacity);
        }

        public static object CreateEmptyTreeRaw<TKey, TValue>(string type, int? capacity = null)
            where TKey : unmanaged, IComparable<TKey> where TValue : unmanaged
        { 
            if (capacity is null)
            {
                return type switch
                {
                    "reference" => new QuickIntervalTree<TKey, TValue>(),
                    "linear" => new LinearIntervalTree<TKey, TValue>(),
                    "light" => new LightIntervalTree<TKey, TValue>(),
                    "quick" => new QuickIntervalTree<TKey, TValue>(),
                    // persistent because exists tests for concurrency
                    "native-linear" => new NativeLinearIntervalTree<TKey, TValue>(Allocator.Persistent),
                    "native-light" => new NativeLightIntervalTree<TKey, TValue>(Allocator.Persistent),
                    "native-quick" => new NativeQuickIntervalTree<TKey, TValue>(Allocator.Persistent),
                    _ => throw new ArgumentException($"Unknown tree type: {type}", nameof(type))
                };
            }

            return type switch
            {
                "reference" => new QuickIntervalTree<TKey, TValue>(),
                "linear" => new LinearIntervalTree<TKey, TValue>(capacity),
                "light" => new LightIntervalTree<TKey, TValue>(capacity),
                "quick" => new QuickIntervalTree<TKey, TValue>(capacity),
                // persistent because exists tests for concurrency
                "native-linear" => new NativeLinearIntervalTree<TKey, TValue>(capacity.Value, Allocator.Persistent),
                "native-light" => new NativeLightIntervalTree<TKey, TValue>(capacity.Value, Allocator.Persistent),
                "native-quick" => new NativeQuickIntervalTree<TKey, TValue>(capacity.Value, Allocator.Persistent),
                _ => throw new ArgumentException($"Unknown tree type: {type}", nameof(type))
            };
        }
    }
}
