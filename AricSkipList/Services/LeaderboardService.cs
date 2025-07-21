using AricSkipList.Enities;
using System.Collections.Concurrent;

namespace AricSkipList.Services
{
    public class LeaderboardService
    {
        private const int MaxLevel = 32; // 最大层数
        private const double Probability = 0.5; // 晋升概率
        private readonly ThreadLocal<Random> _random = new(() => new Random());
        private readonly SkipListNode<Customer> _head;
        private int _currentLevel = 1;
        private readonly ConcurrentDictionary<long, Customer> _customerMap = [];
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
            while (_random.Value.NextDouble() < Probability && level < MaxLevel)
            {
                level++;
            }
            return level;
        }

        private void InsertNode(Customer customer)
        {
            while (true)
            {
                var newNode = new SkipListNode<Customer>(customer, RandomLevel());
                var preNodeArray = new SkipListNode<Customer>[MaxLevel];
                var rankArray = new int[MaxLevel];
                var current = _head;

                //阶段1: 无锁查找阶段
                for (int i = _currentLevel - 1; i >= 0; i--)
                {
                    rankArray[i] = i == _currentLevel - 1 ? 0 : rankArray[i + 1];
                    while (current.GetForward(i) != null && current.GetForward(i).Value.CompareTo(customer) < 0)
                    {
                        rankArray[i] += current.GetSpan(i);
                        current = current.GetForward(i);
                    }
                    preNodeArray[i] = current;
                }

                //前驱节点锁定
                lock (preNodeArray[0].NodeLock)
                {
                    //阶段2：验证前驱节点的后继节点的值是否小于customer,如果是大于等于就说明该节点已被其他线程修改，需要重新回到阶段1
                    var preNodeNextNode = preNodeArray[0].GetForward(0);
                    if (preNodeNextNode != null && preNodeNextNode.Value.CompareTo(customer) < 0)
                    {
                        continue; // 重试
                    }

                    // 处理新层级（加锁保护_head）
                    if (newNode.Level > _currentLevel)
                    {
                        lock (_head.NodeLock)
                        {
                            if (newNode.Level > _currentLevel)
                            {
                                for (int i = _currentLevel; i < newNode.Level; i++)
                                {
                                    preNodeArray[i] = _head;
                                    preNodeArray[i].SetSpan(i, Volatile.Read(ref _count));
                                    rankArray[i] = 0;
                                }
                                _currentLevel = newNode.Level;
                            }
                        }
                    }

                    //  安全更新跳表结构 - 添加null检查
                    for (int i = 0; i < newNode.Level; i++)
                    {
                        var preNode = preNodeArray[i];
                        if (preNode == null)
                        {
                            continue; // 重试整个插入操作
                        }

                        lock (preNode.NodeLock)
                        {
                            newNode.SetForward(i, preNode.GetForward(i));
                            preNode.SetForward(i, newNode);
                            newNode.SetSpan(i, preNode.GetSpan(i) - (rankArray[0] - rankArray[i]));
                            preNode.SetSpan(i, rankArray[0] - rankArray[i] + 1);
                        }
                    }

                    // 安全更新更高层跨度
                    for (int i = newNode.Level; i < _currentLevel; i++)
                    {
                        if (i < preNodeArray.Length && preNodeArray[i] != null)
                        {
                            lock (preNodeArray[i].NodeLock)
                            {
                                // 准确计算需要增加的跨度
                                int increment = rankArray[0] - rankArray[i] + 1;
                                preNodeArray[i].SetSpan(i, preNodeArray[i].GetSpan(i) + increment);
                            }
                        }
                    }

                    Interlocked.Increment(ref _count);
                    break;
                }
            }
        }

        private void RemoveNode(Customer customer)
        {
            while (true)
            {
                var preNodeArray = new SkipListNode<Customer>[MaxLevel];
                var current = _head;

                // 从最高层开始查找节点
                for (int i = _currentLevel - 1; i >= 0; i--)
                {
                    var nextNode = current.GetForward(i);
                    while (nextNode != null && nextNode.Value.CompareTo(customer) < 0)
                    {
                        current = nextNode;
                        nextNode = current.GetForward(i);
                    }
                    preNodeArray[i] = current;
                }

                lock (preNodeArray[0].NodeLock)
                {
                    var nodeToRemove = preNodeArray[0].GetForward(0);
                    if (nodeToRemove == null || !nodeToRemove.Value.Equals(customer))
                        continue;

                    // 更新跳表结构
                    for (int i = 0; i < _currentLevel; i++)
                    {
                        if (preNodeArray[i].GetForward(i) == nodeToRemove)
                        {
                            preNodeArray[i].SetForward(i, nodeToRemove.GetForward(i));
                            preNodeArray[i].SetSpan(i, nodeToRemove.GetSpan(i) + preNodeArray[i].GetSpan(i) - 1);
                        }
                        else
                        {
                            preNodeArray[i].SetSpan(i, preNodeArray[i].GetSpan(i) - 1);
                        }
                    }

                    // 更新当前最大层数
                    while (_currentLevel > 1 && _head.GetForward(_currentLevel - 1) == null)
                    {
                        Interlocked.Decrement(ref _currentLevel);
                    }
                    Interlocked.Decrement(ref _count);
                    break;
                }
            }
        }

        private void CheckScore(decimal score)
        {
            if (score < -1000 || score > 1000)
                throw new ArgumentOutOfRangeException(nameof(score), "Score must be between -1000 and 1000");
        }

        public int SortedCount => Volatile.Read(ref _count);
        public int Count => _customerMap.Count;

        // 插入或更新节点
        public decimal AddOrUpdate(long customerId, decimal scoreChange)
        {
            CheckScore(scoreChange);

            while (true)
            {
                if (_customerMap.TryGetValue(customerId, out var existingCustomer))
                {
                    lock (existingCustomer)
                    {
                        //只有大于》0的分数才会在排行榜上显示
                        if (existingCustomer.Score > 0)
                            RemoveNode(existingCustomer);

                        existingCustomer.Score += scoreChange;

                        if (existingCustomer.Score > 0)
                            InsertNode(existingCustomer);
                        return existingCustomer.Score;
                    }
                }
                else
                {
                    // 插入新节点
                    var newCustomer = new Customer() { CustomerID = customerId, Score = scoreChange };
                    if (_customerMap.TryAdd(customerId, newCustomer))
                    {
                        if (scoreChange > 0)
                            InsertNode(newCustomer);
                        return scoreChange;
                    }
                }
            }
        }

        // 获取排名范围内的客户
        public List<CustomerDto> GetByRank(int startRank, int endRank)
        {
            if (startRank < 1 || endRank < startRank || startRank > SortedCount)
                throw new ArgumentException("Invalid rank range");

            var result = new List<CustomerDto>(endRank - startRank + 1);
            var current = _head;
            int currentRank = 0;

            // 快速定位到起始位置
            for (int i = _currentLevel - 1; i >= 0; i--)
            {
                while (current.GetForward(i) != null && currentRank + current.GetSpan(i) < startRank)
                {
                    currentRank += current.GetSpan(i);
                    current = current.GetForward(i);
                }
            }

            // 移动到起始位置的下一个节点
            current = current.GetForward(0);
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
                current = current.GetForward(0);
                currentRank++;
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

            // 查找目标节点的排名
            int targetRank = GetRank(targetCustomer);
            if (targetRank == -1)
                return [];

            // 计算范围
            int startRank = Math.Max(1, targetRank - highCount);
            int endRank = Math.Min(_count, targetRank + lowCount);

            return GetByRank(startRank, endRank);
        }

        // 获取节点的排名
        private int GetRank(Customer customer)
        {
            var current = _head;
            int rank = 0;

            for (int i = _currentLevel - 1; i >= 0; i--)
            {
                while (current.GetForward(i) != null && current.GetForward(i).Value.CompareTo(customer) <= 0)
                {
                    if (current.GetForward(i).Value == customer)
                    {
                        rank += current.GetSpan(i);
                        return rank;
                    }

                    rank += current.GetSpan(i);
                    current = current.GetForward(i);
                }
            }

            return -1; // 节点不存在
        }
    }
}