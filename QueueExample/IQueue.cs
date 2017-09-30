using System;

namespace QueueExample
{
    public interface IQueue<T> : IDisposable
    {
        /// <summary> Add specified <paramref name="item"/> to the queue 
        /// </summary>
        /// <param name="item">item</param>
        void Push(T item);

        /// <summary> Returns item from the queue 
        /// </summary>
        /// <returns>item</returns>
        /// <remarks>if queue is empty then waits for new item. In such case you cannot be sure about priority of concurrent calls to Pop</remarks>
        T Pop();
    }
}
