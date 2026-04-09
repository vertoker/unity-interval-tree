using NUnit.Framework;
using UnityEngine;

namespace Jamarino.IntervalTree.Tests
{
    public class BaseTests
    {
        [Test]
        public void SimpleTest()
        {
            // create a tree
            var tree = new LightIntervalTree<int, short>();

            // add intervals (from, to, value)
            tree.Add(10, 30, 1);
            tree.Add(20, 40, 2);
            tree.Add(25, 35, 3);

            // query
            
            var enumerable = tree.Query(11); // result is {1}
            Debug.Log(string.Join(", ", enumerable));
            
            enumerable = tree.Query(32); // result is {2, 3}
            Debug.Log(string.Join(", ", enumerable));
            
            enumerable = tree.Query(27); // result is {1, 2, 3}
            Debug.Log(string.Join(", ", enumerable));

            // query range
            
            enumerable = tree.Query(5, 20); // result is {1, 2}
            Debug.Log(string.Join(", ", enumerable));
            
            enumerable = tree.Query(26, 28); // result is {1, 2, 3}
            Debug.Log(string.Join(", ", enumerable));
            
            enumerable = tree.Query(1, 50); // result is {1, 2, 3}
            Debug.Log(string.Join(", ", enumerable));

            // note: result order is not guaranteed
        }
    }
}