using AricSortedSet.Enities;

namespace AricSortedSet.Services
{
    public class LeaderboardService
    {
        private readonly SortedSet<Customer> _leaderboard = new();
        private readonly Dictionary<long, Customer> _customers = new();
        private readonly ReaderWriterLockSlim _lock = new();

        public int SortedCount => _leaderboard.Count;
        public int Count => _customers.Count;

        private void CheckScore(decimal score)
        {
            if (score < -1000 || score > 1000)
                throw new ArgumentOutOfRangeException(nameof(score), "Score must be between -1000 and 1000");
        }

        public decimal AddOrUpdate(long customerId, decimal scoreChange)
        {
            _lock.EnterWriteLock();
            try
            {
                CheckScore(scoreChange);

                //只有大于》0的分数才会在排行榜上显示
                if (_customers.TryGetValue(customerId, out var customer))
                {
                    if (customer.Score > 0)
                        _leaderboard.Remove(customer);

                    customer.Score += scoreChange;
                    CheckScore(customer.Score);

                    if (customer.Score > 0)
                        _leaderboard.Add(customer);
                }
                else
                {
                    customer = new Customer { CustomerID = customerId, Score = scoreChange };
                    _customers[customerId] = customer;
                    if (customer.Score > 0)
                        _leaderboard.Add(customer);
                }
                return customer.Score;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public List<CustomerDto> GetByRank(int startRank, int endRank)
        {
            _lock.EnterReadLock();
            try
            {
                if (startRank < 1 || endRank < startRank)
                    throw new ArgumentException("Invalid rank range");

                var takeCount = endRank - startRank + 1;
                var result = new List<CustomerDto>(takeCount);
                using var enumerator = _leaderboard.GetEnumerator();
                for (int i = 0; i < startRank && enumerator.MoveNext(); i++) ;
                for (int i = 0; i < takeCount && enumerator.MoveNext(); i++)
                {
                    var c = enumerator.Current;
                    result.Add(new CustomerDto
                    {
                        CustomerID = c.CustomerID,
                        Score = c.Score,
                        Rank = startRank + i
                    });
                }
                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public List<CustomerDto> GetCustomerWithNeighbors(long customerId, int high = 0, int low = 0)
        {
            _lock.EnterReadLock();
            try
            {
                if (!_customers.TryGetValue(customerId, out var customer))
                    throw new KeyNotFoundException("Customer not found");
                if (customer.Score <= 0)
                    return [];

                var lowerView = _leaderboard.GetViewBetween(_leaderboard.Min, customer);
                int index = lowerView.Count - 1;

                int start = Math.Max(0, index - high);
                int end = Math.Min(_leaderboard.Count - 1, index + low);
                int takeCount = end - start + 1;

                var result = new List<CustomerDto>(takeCount);
                using (var enumerator = _leaderboard.GetEnumerator())
                {
                    for (int i = 0; i < start && enumerator.MoveNext(); i++) ;
                    for (int i = 0; i < takeCount && enumerator.MoveNext(); i++)
                    {
                        var c = enumerator.Current;
                        result.Add(new CustomerDto
                        {
                            CustomerID = c.CustomerID,
                            Score = c.Score,
                            Rank = start + i + 1
                        });
                    }
                }

                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}