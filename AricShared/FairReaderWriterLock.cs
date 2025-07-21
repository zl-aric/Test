namespace AricShared
{

    public class FairReaderWriterLock
    {
        private readonly SemaphoreSlim _readSemaphore = new(1, 1);
        private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
        private int _readers;
        private int _writersWaiting;

        public void EnterReadLock()
        {
            _readSemaphore.Wait();  // 步骤1：获取读信号量（互斥访问读者计数）
            try
            {
                if (Interlocked.Increment(ref _readers) == 1) // 步骤2：增加读者计数
                {
                    _writeSemaphore.Wait();  // 步骤3：如果是第一个读者，获取写信号量
                }
            }
            finally
            {
                _readSemaphore.Release();  // 步骤4：释放读信号量
            }
        }

        public void ExitReadLock()
        {
            _readSemaphore.Wait();  // 步骤1：获取读信号量
            try
            {
                if (Interlocked.Decrement(ref _readers) == 0) // 步骤2：减少读者计数
                {
                    _writeSemaphore.Release();  // 步骤3：如果是最后一个读者，释放写信号量
                }
            }
            finally
            {
                _readSemaphore.Release();  // 步骤4：释放读信号量
            }
        }

        public void EnterWriteLock()
        {
            Interlocked.Increment(ref _writersWaiting);  // 步骤1：增加等待写者计数
            _writeSemaphore.Wait();  // 步骤2：获取写信号量（阻塞直到可用）
            Interlocked.Decrement(ref _writersWaiting);  // 步骤3：减少等待写者计数
        }

        public void ExitWriteLock()
        {
            _writeSemaphore.Release();  // 释放写信号量
        }
    }
}

