using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Jamarino.IntervalTree.Native
{
    public static class NativeReverseExtension
    {
        public static unsafe void Reverse<T>(this NativeSlice<T> slice)
            where T : unmanaged, IComparable<T>
        {
            var ptr = (T*)slice.GetUnsafePtr();
            var len = slice.Length;

            CheckStrideMatchesSize<T>(slice.Stride);
            Reverse<T>(ptr, len);
        }

        private static unsafe void Reverse<T>(void* array, int length)
            where T : unmanaged
        {
            if (array == null || length <= 1)
                return;
    
            var left = 0;
            var right = length - 1;
    
            while (left < right)
            {
                SwapStruct<T>(array, left, right);
                left++;
                right--;
            }
        }

        private static unsafe void SwapStruct<T>(void* array, int lhs, int rhs)
            where T : unmanaged
        {
            var val = UnsafeUtility.ReadArrayElement<T>(array, lhs);
            UnsafeUtility.WriteArrayElement(array, lhs, UnsafeUtility.ReadArrayElement<T>(array, rhs));
            UnsafeUtility.WriteArrayElement(array, rhs, val);
        }
        
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckStrideMatchesSize<T>(int stride) where T : unmanaged
        {
            if (stride != UnsafeUtility.SizeOf<T>())
            {
                throw new InvalidOperationException("Sort requires that stride matches the size of the source type");
            }
        }
    }
}