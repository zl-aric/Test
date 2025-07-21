using AricSortedSet.Enities;

namespace AricSortedSet.Services
{
    public class LeaderboardService
    {
        private readonly SortedSet<Customer> _leaderboard = new();
        private readonly Dictionary<long, Customer> _customers = new();
        private readonly ReaderWriterLockSlim _lock = new();
        private List<Customer> _cachedRanking = new();
        private bool _rankingDirty = true;

        public int SortedCount => _leaderboard.Count;
        public int Count => _customers.Count;

        private void CheckScore(decimal score)
        {
            if (score < -1000 || score > 1000)
                throw new ArgumentOutOfRangeException(nameof(score), "Score must be between -1000 and 1000");
        }

        private void UpdateRankingCache()
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_rankingDirty) return;

                _cachedRanking = _leaderboard.ToList();
                _rankingDirty = false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public decimal AddOrUpdate(long customerId, decimal scoreChange)
        {
            _lock.EnterWriteLock();
            try
            {
                CheckScore(scoreChange);

                // 标记排名缓存需要更新
                _rankingDirty = true;
                //只有大于》0的分数才会在排行榜上显示
                if (_customers.TryGetValue(customerId, out var customer))
                {
                    if (customer.Score > 0)
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
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public List<CustomerDto> GetByRank(int startRank, int endRank)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (startRank < 1 || endRank < startRank || startRank > SortedCount)
                    throw new ArgumentException("Invalid rank range");

                UpdateRankingCache();

                var takeCount = Math.Min(SortedCount, endRank) - startRank + 1;
                var result = new List<CustomerDto>(takeCount);
                using var enumerator = _leaderboard.GetEnumerator();
                // 直接从缓存列表中获取
                for (int i = 0; i < takeCount; i++)
                {
                    var index = startRank - 1 + i;
                    if (index >= _cachedRanking.Count) break;

                    var c = _cachedRanking[index];
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
                _lock.ExitUpgradeableReadLock();
            }
        }

        public List<CustomerDto> GetCustomerWithNeighbors(long customerId, int high = 0, int low = 0)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (!_customers.TryGetValue(customerId, out var customer))
                    throw new KeyNotFoundException("Customer not found");
                if (customer.Score <= 0)
                    return [];

                UpdateRankingCache();

                int index = _cachedRanking.BinarySearch(customer);

                int start = Math.Max(0, index - high);
                int end = Math.Min(_cachedRanking.Count - 1, index + low);
                int takeCount = end - start + 1;

                var result = new List<CustomerDto>(takeCount);
                for (int i = 0; i < takeCount; i++)
                {
                    var c = _cachedRanking[start + i];
                    result.Add(new CustomerDto
                    {
                        CustomerID = c.CustomerID,
                        Score = c.Score,
                        Rank = start + i + 1
                    });
                }

                return result;
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }
    }
}