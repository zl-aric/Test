using AricTest.Enities;
using System.Collections.Concurrent;

namespace AricTest.Services
{
    public class LeaderboardService
    {
        private readonly SortedSet<Customer> _leaderboard = new();
        private readonly Dictionary<long, Customer> _customers = new();
        private readonly object _lock = new();

        public decimal UpdateScore(long customerId, decimal scoreChange)
        {
            if (scoreChange < -1000 || scoreChange > 1000)
                throw new ArgumentOutOfRangeException(nameof(scoreChange), "Score must be between -1000 and 1000");

            lock (_lock)
            {
                //只有大于》0的分数才会在排行榜上显示
                if (_customers.TryGetValue(customerId, out var customer))
                {
                    _leaderboard.Remove(customer);
                    customer.Score += scoreChange;
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
        }

        public List<CustomerDto> GetByRank(int startRank, int endRank)
        {
            if (startRank < 1 || endRank < startRank)
                throw new ArgumentException("Invalid rank range");

            lock (_lock)
            {
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
                        Rank = startRank + i + 1
                    });
                }
                return result;
            }
        }

        public List<CustomerDto> GetCustomerWithNeighbors(long customerId, int high = 0, int low = 0)
        {
            lock (_lock)
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
        }

    }
}

