namespace AricShared
{
    public class SkipListNode<T> where T : IComparable<T>
    {
        public T Value { get; set; }

        public SkipListNode<T>[] Forward { get; set; }
        public int[] Span { get; set; }

        public SkipListNode(T value, int level)
        {
            Value = value;
            Forward = new SkipListNode<T>[level];
            Span = new int[level];
        }
    }
}