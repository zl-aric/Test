using AricSkipList.Enities;

namespace AricSkipList.Services
{
    public class LeaderboardService
    {
        private const int MaxLevel = 32; // 最大层数
        private const double Probability = 0.5; // 晋升概率
        private readonly Random _random = new();
        private readonly SkipListNode<Customer> _head;
        private int _currentLevel = 1;
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly Dictionary<long, Customer> _customerMap = [];
        private int _count = 0; // 总节点数

        public LeaderboardService()
        {
            _head = new SkipListNode<Customer>(new Customer() { CustomerID = long.MinValue, Score = decimal.MinValue }, MaxLevel);
            for (int i = 0; i < MaxLevel; i++)
            {
                _head.Forward[i] = null;
                _head.Span[i] = 0;
            }
        }

        // 随机生成节点层数
        private int RandomLevel()
        {
            int level = 1;
            while (_random.NextDouble() < Probability && level < MaxLevel)
            {
                level++;
            }
            return level;
        }

        private void InsertNode(SkipListNode<Customer> node)
        {
            //用于记录当前节点在每一层的前驱节点
            var preNodeArray = new SkipListNode<Customer>[MaxLevel];
            //用于记录查找时,每层的累计跨度,方便计算排名,
            //当下标i=0时，代表当前节点的排名，
            //当下标i>0时，代表当前节点在第i层的累计跨度
            var rankArray = new int[MaxLevel];
            var current = _head;

            // 从最高层开始查找插入位置，找到每层的前驱节点
            for (int i = _currentLevel - 1; i >= 0; i--)
            {
                //如果是最高层，rankArray[i] = 0，否则 rankArray[i] = rankArray[i + 1] 继承上一层的排名
                rankArray[i] = i == _currentLevel - 1 ? 0 : rankArray[i + 1];
                while (current.Forward[i] != null && current.Forward[i].CompareTo(node) < 0)
                {
                    rankArray[i] += current.Span[i];
                    current = current.Forward[i];
                }
                preNodeArray[i] = current;
            }

            // 如果新节点层数高于当前最大层数，构建新的层级
            if (node.Forward.Length > _currentLevel)
            {
                for (int i = _currentLevel; i < node.Forward.Length; i++)
                {
                    preNodeArray[i] = _head;
                    preNodeArray[i].Span[i] = _count;
                    rankArray[i] = 0;
                }
                _currentLevel = node.Forward.Length;
            }

            // 更新跳表结构
            for (int i = 0; i < node.Forward.Length; i++)
            {
                // 在每层插入新节点
                node.Forward[i] = preNodeArray[i].Forward[i];
                preNodeArray[i].Forward[i] = node;

                // 更新当前节点在每层的跨度
                node.Span[i] = preNodeArray[i].Span[i] - (rankArray[0] - rankArray[i]);
                preNodeArray[i].Span[i] = rankArray[0] - rankArray[i] + 1;
            }

            // 更新更高层的跨度
            for (int i = node.Forward.Length; i < _currentLevel; i++)
            {
                preNodeArray[i].Span[i]++;
            }

            _count++;
        }

        private void RemoveNode(SkipListNode<Customer> node)
        {
            var preNodeArray = new SkipListNode<Customer>[MaxLevel];
            var current = _head;

            // 从最高层开始查找节点
            for (int i = _currentLevel - 1; i >= 0; i--)
            {
                while (current.Forward[i] != null && current.Forward[i].CompareTo(node) < 0)
                {
                    current = current.Forward[i];
                }
                preNodeArray[i] = current;
            }

            // 验证找到的节点是否正确
            if (current.Forward[0] != node)
            {
                return; // 节点不存在
            }

            // 更新跳表结构
            for (int i = 0; i < _currentLevel; i++)
            {
                //判断每层前驱节点的下一个节点是否等于node,如果等于就需要删除
                if (preNodeArray[i].Forward[i] == node)
                {
                    preNodeArray[i].Span[i] += node.Span[i] - 1;
                    preNodeArray[i].Forward[i] = node.Forward[i];
                }
                else
                {
                    preNodeArray[i].Span[i]--;
                }
            }

            // 更新当前最大层数
            while (_currentLevel > 1 && _head.Forward[_currentLevel - 1] == null)
            {
                _currentLevel--;
            }

            _count--;
        }

        private void CheckScore(decimal score)
        {
            if (score < -1000 || score > 1000)
                throw new ArgumentOutOfRangeException(nameof(score), "Score must be between -1000 and 1000");
        }

        public int SortedCount => _count;
        public int Count => _customerMap.Count;

        // 插入或更新节点
        public decimal AddOrUpdate(long customerId, decimal scoreChange)
        {
            CheckScore(scoreChange);
            _lock.EnterWriteLock();
            try
            {
                if (_customerMap.TryGetValue(customerId, out var existingCustomer))
                {
                    //只有大于》0的分数才会在排行榜上显示
                    if (existingCustomer.Score > 0)
                        RemoveNode(new SkipListNode<Customer>(existingCustomer, RandomLevel()));

                    existingCustomer.Score += scoreChange;

                    if (existingCustomer.Score > 0)
                        InsertNode(new SkipListNode<Customer>(existingCustomer, RandomLevel()));
                    return existingCustomer.Score;
                }
                else
                {
                    // 插入新节点
                    var newCustomer = new Customer() { CustomerID = customerId, Score = scoreChange };
                    _customerMap[customerId] = newCustomer;
                    // 新用户分数必须>0才能加入排行榜
                    if (scoreChange > 0)
                        InsertNode(new SkipListNode<Customer>(newCustomer, RandomLevel()));
                    return scoreChange;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        // 获取排名范围内的客户
        public List<CustomerDto> GetByRank(int startRank, int endRank)
        {
            _lock.EnterReadLock();
            try
            {
                return GetByRankInternal(startRank, endRank);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }


        private List<CustomerDto> GetByRankInternal(int startRank, int endRank)
        {
            if (startRank < 1 || endRank < startRank || startRank > SortedCount)
                throw new ArgumentException("Invalid rank range");


            var result = new List<CustomerDto>();
            var current = _head;
            int currentRank = 0;
            int count = 0;

            // 快速定位到起始位置
            for (int i = _currentLevel - 1; i >= 0; i--)
            {
                while (current.Forward[i] != null && currentRank + current.Span[i] < startRank)
                {
                    currentRank += current.Span[i];
                    current = current.Forward[i];
                }
            }

            // 移动到起始位置的下一个节点
            current = current.Forward[0];
            currentRank++;

            // 收集范围内的节点
            while (current != null && currentRank <= endRank)
            {
                result.Add(new CustomerDto
                {
                    CustomerID = current.Value.CustomerID,
                    Score = current.Value.Score,
                    Rank = currentRank
                });
                current = current.Forward[0];
                currentRank++;
                count++;
            }

            return result;
        }

        // 获取客户及其邻居
        public List<CustomerDto> GetCustomerWithNeighbors(long customerId, int highCount = 0, int lowCount = 0)
        {
            if (!_customerMap.TryGetValue(customerId, out var targetCustomer))
                throw new KeyNotFoundException("Customer not found");
            if (targetCustomer.Score <= 0)
                return [];

            _lock.EnterReadLock();
            try
            {
                // 查找目标节点的排名
                int targetRank = GetRank(targetCustomer);
                if (targetRank == -1)
                    return [];

                // 计算范围
                int startRank = Math.Max(1, targetRank - highCount);
                int endRank = Math.Min(_count, targetRank + lowCount);

                return GetByRankInternal(startRank, endRank);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // 获取节点的排名
        private int GetRank(Customer customer)
        {
            var current = _head;
            int rank = 0;

            for (int i = _currentLevel - 1; i >= 0; i--)
            {
                while (current.Forward[i] != null && current.Forward[i].Value.CompareTo(customer) <= 0)
                {
                    if (current.Forward[i].Value == customer)
                    {
                        rank += current.Span[i];
                        return rank;
                    }

                    rank += current.Span[i];
                    current = current.Forward[i];
                }
            }

            return -1; // 节点不存在
        }
    }
}