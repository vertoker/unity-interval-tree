using System;

namespace Jamarino.IntervalTree
{
    public interface IBuildIntervalTree<TKey, TValue> : IIntervalTree<TKey, TValue>
        where TKey : IComparable<TKey>
    {
        /// <summary>
        /// Build the underlying tree structure.
        /// A build is automatically performed, if needed, on the first query after altering the tree.
        /// </summary>
        public void Build();
    }
}