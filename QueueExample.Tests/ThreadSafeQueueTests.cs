using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace QueueExample.Tests
{
    [TestClass]
    public class ThreadSafeQueueTests
    {
        const int TaskGuardTimeout = 1000;

        private IQueue<int> target;

        [TestInitialize]
        public void SetUp()
        {
            this.target = new ThreadSafeQueue<int>();
        }

        [TestMethod]
        public void Pop_AfterPush_ShouldReturn()
        {
            // Arrange
            const int expected = 10;
            target.Push(expected);

            // Act
            var actual = target.Pop();

            // Assert
            actual.ShouldBeEquivalentTo(expected);
        }

        [TestMethod]
        public void Pop_EmptyQueueAndThenPush_ShouldWaitAndReturn()
        {
            // Arrange
            const int expected = 15;
            int actual = 0;

            var popTaskTrigger = new Trigger();

            ThreadPool.QueueUserWorkItem(state => actual = Pop(this.target, popTaskTrigger));

            var isPopTaskStarted = popTaskTrigger.TaskStarted.WaitOne(TaskGuardTimeout);

            // Act
            this.target.Push(expected);

            // Assert
            var isPopTaskFinished = popTaskTrigger.TaskFinished.WaitOne(TaskGuardTimeout);

            isPopTaskStarted.Should().BeTrue();
            isPopTaskFinished.Should().BeTrue();
            actual.ShouldBeEquivalentTo(expected);
        }

        [TestMethod]
        public void Pop_ConcurrentCalls_ShouldReturnUnordered()
        {
            // Arrange
            var expected = new List<int> { 10, 20 };

            int actualA = 0;
            int actualB = 0;

            var popTaskATrigger = new Trigger();
            var popTaskBTrigger = new Trigger();

            ThreadPool.QueueUserWorkItem(state => actualA = Pop(this.target, popTaskATrigger));

            ThreadPool.QueueUserWorkItem(state => actualB = Pop(this.target, popTaskBTrigger));

            var isPopTaskAStarted = popTaskATrigger.TaskStarted.WaitOne(TaskGuardTimeout);
            var isPopTaskBStarted = popTaskBTrigger.TaskStarted.WaitOne(TaskGuardTimeout);

            // Act
            expected.ForEach(item => target.Push(item));

            // Assert
            var isPopTaskAFinished = popTaskATrigger.TaskFinished.WaitOne(TaskGuardTimeout);
            var isPopTaskBFinished = popTaskBTrigger.TaskFinished.WaitOne(TaskGuardTimeout);

            isPopTaskAStarted.Should().BeTrue();
            isPopTaskBStarted.Should().BeTrue();

            isPopTaskAFinished.Should().BeTrue();
            isPopTaskBFinished.Should().BeTrue();

            expected.Remove(actualA).Should().BeTrue(because: $"{actualA} must be at expected collection");
            expected.Remove(actualB).Should().BeTrue(because: $"{actualB} must be at expected collection");
        }

        [TestMethod]
        public void Dispose_HangedPop_PopShouldThrow()
        {
            // Arrange
            var popTaskTrigger = new Trigger();

            Exception actual = null;

            ThreadPool.QueueUserWorkItem(state =>
            {
                popTaskTrigger.TaskStarted.Set();

                try
                {
                    this.target.Pop();
                }
                catch (Exception e)
                {
                    actual = e;
                }

                popTaskTrigger.TaskFinished.Set();
            });

            var isPopTaskStarted = popTaskTrigger.TaskStarted.WaitOne(TaskGuardTimeout);

            // HACK gtsaplin: make sure that Pop started
            Thread.Sleep(100);

            // Act
            this.target.Dispose();

            // Assert
            var isPopTaskFinished = popTaskTrigger.TaskFinished.WaitOne(TaskGuardTimeout);

            isPopTaskStarted.Should().BeTrue();
            isPopTaskFinished.Should().BeTrue();

            actual.Should().BeOfType<ObjectDisposedException>(because: "Queue was disposed while waiting inside Pop");
        }

        [TestMethod]
        public void Pop_OnDisposedObject_ShouldThrow()
        {
            // Arrange
            this.target.Dispose();

            // Act
            Action action = () => this.target.Pop();

            // Assert
            action.ShouldThrow<ObjectDisposedException>();
        }

        [TestMethod]
        public void Push_OnDisposedObject_ShouldThrow()
        {
            // Arrange
            this.target.Dispose();

            // Act
            Action action = () => this.target.Push(100);

            // Assert
            action.ShouldThrow<ObjectDisposedException>();
        }

        [TestMethod]
        public void Dispose_OnDisposedObject_ShouldNotThrow()
        {
            // Arrange
            this.target.Dispose();

            // Act
            Action action = () => this.target.Dispose();

            // Assert
            action.ShouldNotThrow();
        }

        private static T Pop<T>(IQueue<T> queue, Trigger trigger)
        {
            trigger.TaskStarted.Set();

            var result = queue.Pop();

            trigger.TaskFinished.Set();

            return result;
        }

        private class Trigger
        {
            public Trigger()
            {
                this.TaskStarted = new ManualResetEvent(false);
                this.TaskFinished = new ManualResetEvent(false);
            }

            public EventWaitHandle TaskStarted { get; }

            public EventWaitHandle TaskFinished { get; }
        }
    }
}
