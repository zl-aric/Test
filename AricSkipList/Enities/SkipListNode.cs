namespace AricSkipList.Enities
{
    public class SkipListNode<T> where T : IComparable<T>
    {
        public T Value { get; set; }

        public SkipListNode<T>[] Forward { get; set; }
        public int[] Span { get; set; }

        public readonly object NodeLock = new();
        public readonly int Level;

        public SkipListNode(T value, int level)
        {
            Value = value;
            Forward = new SkipListNode<T>[level];
            Span = new int[level];
            Level = level;
        }

        public SkipListNode<T> GetForward(int level) => Volatile.Read(ref Forward[level]);

        public void SetForward(int level, SkipListNode<T> node) => Volatile.Write(ref Forward[level], node);

        public int GetSpan(int level) => Volatile.Read(ref Span[level]);

        public void SetSpan(int level, int value) => Volatile.Write(ref Span[level], value);
    }
}