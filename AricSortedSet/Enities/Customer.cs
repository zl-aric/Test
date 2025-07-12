namespace AricTest.Enities
{
    public class Customer : IComparable<Customer>
    {
        public long CustomerID { get; set; }
        public decimal Score { get; set; }

        public int CompareTo(Customer? other)
        {
            if (other == null) return 1;

            int scoreComparison = other.Score.CompareTo(Score);
            if (scoreComparison != 0)
                return scoreComparison;

            return CustomerID.CompareTo(other.CustomerID);
        }

        public override bool Equals(object? obj)
        {
            return obj is Customer customer && CustomerID == customer.CustomerID;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CustomerID);
        }
    }
}