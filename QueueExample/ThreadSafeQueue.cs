using System;
using System.Collections.Generic;
using System.Threading;

namespace QueueExample
{
    public class ThreadSafeQueue<T> : IQueue<T>
    {
        private readonly ManualResetEvent trigger = new ManualResetEvent(false);
        private readonly object sync = new object();
        private readonly Queue<T> queue;

        public ThreadSafeQueue()
        {
            this.queue = new Queue<T>();
        }

        public void Push(T item)
        {
            this.ValidateNonDisposed();

            lock (this.sync)
            {
                this.queue.Enqueue(item);
                this.trigger.Set();
            }
        }

        public T Pop()
        {
            this.ValidateNonDisposed();

            do
            {
                // HACK gtsaplin: do not analyze return value because without timeout cannot be false condition of WaitOne()
                var nomatter = this.trigger.WaitOne();

                this.ValidateNonDisposed();

                lock (this.sync)
                {
                    try
                    {
                        return this.queue.Dequeue();
                    }
                    catch (InvalidOperationException)
                    {
                        this.trigger.Reset();
                    }
                }
            } while (true);
        }

        private int disposedFlag = 0;
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposedFlag, 1, 0) != 0)
            {
                return;
            }

            // HACK gtsaplin: push through all Pop callers
            this.trigger.Set();
        }

        private void ValidateNonDisposed()
        {
            if (disposedFlag == 1)
            {
                throw new ObjectDisposedException(nameof(ThreadSafeQueue<T>));
            }
        }
    }
}
