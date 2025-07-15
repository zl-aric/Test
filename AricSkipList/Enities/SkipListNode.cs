namespace AricSkipList.Enities
{
    public class SkipListNode<T> : IComparable<SkipListNode<T>>
        where T : IComparable<T>
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

        public int CompareTo(SkipListNode<T>? other)
        {
            if (other == null) return 1;
            return Value.CompareTo(other.Value);
        }

        public override bool Equals(object? obj)
        {
            return obj is SkipListNode<T> node && Value.Equals(node.Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}