namespace vertoker.UnityIntervalTree.Native
{
    public struct ValueIndexed<TValue> where TValue : unmanaged
    {
        public int Index;
        public TValue Value;

        public ValueIndexed(int index, TValue value)
        {
            Index = index;
            Value = value;
        }
    }
}