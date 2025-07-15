namespace AricSortedSet.Services.Tests
{
    [TestClass()]
    public class LeaderboardServiceTests
    {
        [TestMethod()]
        public void ScoreRangeTest()
        {
            LeaderboardService _leaderboardService = new();
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _leaderboardService.AddOrUpdate(1, 1001));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _leaderboardService.AddOrUpdate(1, -1001));
            Assert.AreEqual(_leaderboardService.AddOrUpdate(1, 20), 20);
            Assert.AreEqual(_leaderboardService.AddOrUpdate(1, 20), 40);
            Assert.AreEqual(_leaderboardService.AddOrUpdate(1, 100), 140);
            Assert.AreEqual(_leaderboardService.AddOrUpdate(1, -20), 120);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _leaderboardService.AddOrUpdate(1, 900));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => _leaderboardService.AddOrUpdate(1, -1200));
        }


        [TestMethod()]
        public void ConcurrentTest()
        {
            LeaderboardService _leaderboardService = new();
            int count = 10000000;
            Parallel.For(1, count + 1, i =>
            {
                decimal score = GenerateRandomDecimal();
                _leaderboardService.AddOrUpdate(i, score);
            });

            Assert.AreEqual(_leaderboardService.Count, count);
            Assert.IsTrue(_leaderboardService.SortedCount <= count);
        }


        [TestMethod]
        public void GetRankTest()
        {
            var leaderboardService = new LeaderboardService();
            int count = 1000000;

            Parallel.For(1, count + 1, i =>
            {
                decimal score = GenerateRandomDecimal();
                leaderboardService.AddOrUpdate(i, score);
            });

            // 阶段2: 验证无效参数异常
            Assert.ThrowsException<ArgumentException>(() => leaderboardService.GetByRank(0, 1));
            Assert.ThrowsException<ArgumentException>(() => leaderboardService.GetByRank(2, 1));
            Assert.ThrowsException<ArgumentException>(() => leaderboardService.GetByRank(-1, 100));

            // 阶段3: 验证排名连续性
            int testSize = 1000;
            int realSize = Math.Min(testSize, leaderboardService.SortedCount);
            var results = leaderboardService.GetByRank(1, testSize);
            Assert.AreEqual(realSize, results.Count, "返回结果数量不正确");

            // 验证排名是否正确递增且连续
            for (int i = 0; i < results.Count; i++)
            {
                Assert.AreEqual(i + 1, results[i].Rank, $"排名不连续，索引{i}的排名为{results[i].Rank}");
            }

            // 阶段4: 验证排序正确性
            for (int i = 0; i < results.Count - 1; i++)
            {
                var current = results[i];
                var next = results[i + 1];

                // 验证分数降序排列
                Assert.IsTrue(current.Score >= next.Score, $"分数未正确排序: {current.Score} < {next.Score}");

                // 如果分数相同，验证CustomerID升序
                if (current.Score == next.Score)
                {
                    Assert.IsTrue(current.CustomerID < next.CustomerID, $"相同分数时CustomerID未正确排序: {current.CustomerID} > {next.CustomerID}");
                }
            }

            // 阶段5: 随机抽样验证
            var randomRanges = new[]
            {
                (1, 10),
                (count/2, count/2 + 100),
                (count - 50, count)
            };

            foreach (var (start, end) in randomRanges)
            {
                var rangeResults = leaderboardService.GetByRank(start, end);
                if (leaderboardService.SortedCount < start)
                    realSize = 0;
                else
                    realSize = Math.Min(end - start + 1, leaderboardService.SortedCount - start + 1);
                Assert.AreEqual(realSize, rangeResults.Count, $"范围{start}-{end}结果数量不正确");

                for (int i = 0; i < rangeResults.Count; i++)
                {
                    Assert.AreEqual(start + i, rangeResults[i].Rank, $"范围{start}-{end}中索引{i}的排名不正确");
                }
            }
        }

        [TestMethod()]
        public void GetCustomerWithNeighborsTest()
        {
            var leaderboardService = new LeaderboardService();
            int count = 1000000;
            decimal step = 1.0m / 1000;

            Parallel.For(1, count + 1, i =>
            {
                decimal score = step * i;
                leaderboardService.AddOrUpdate(i, score);
            });

            //阶段1: 未知的ID验证
            Assert.ThrowsException<KeyNotFoundException>(() => leaderboardService.GetCustomerWithNeighbors(count + 1));
            //阶段2: 分数小于0的ID验证
            leaderboardService.AddOrUpdate(count + 1, -1);
            Assert.IsFalse(leaderboardService.GetCustomerWithNeighbors(count + 1).Any());
            //阶段3,左边界测试
            var leftNeighbor = leaderboardService.GetCustomerWithNeighbors(count);
            Assert.AreEqual(1, leftNeighbor.Count, "左边界测试失败，返回结果数量不正确");
            leftNeighbor = leaderboardService.GetCustomerWithNeighbors(count, 100);
            Assert.AreEqual(1, leftNeighbor.Count, "左边界测试失败，返回结果数量不正确");

            //阶段4,右边界测试
            var rightNeighbor = leaderboardService.GetCustomerWithNeighbors(1);
            Assert.AreEqual(1, rightNeighbor.Count, "右边界测试失败，返回结果数量不正确");
            rightNeighbor = leaderboardService.GetCustomerWithNeighbors(1, 0, 100);
            Assert.AreEqual(1, rightNeighbor.Count, "右边界测试失败，返回结果数量不正确");

            //阶段5,中间测试
            var middleNeighbor = leaderboardService.GetCustomerWithNeighbors(count / 2, 100, 100);
            Assert.IsTrue(middleNeighbor.Count > 0, "中间测试失败，返回结果数量不正确");
            Assert.IsTrue(middleNeighbor.Count == 201, "中间测试失败，返回结果数量超过预期范围");
            for (int i = 0; i < middleNeighbor.Count - 1; i++)
            {
                Assert.IsTrue(middleNeighbor[i].Rank < middleNeighbor[i + 1].Rank, "中间测试失败，排名不连续");
            }

        }


        public decimal GenerateRandomDecimal()
        {
            Random random = new Random();
            int randomInt = random.Next(-1000000, 1000001);
            return (decimal)randomInt / 1000;
        }

        public decimal GetPositiveDecimal()
        {
            Random random = new Random();
            int randomInt = random.Next(1, 1000001);
            return (decimal)randomInt / 1000;
        }
    }
}