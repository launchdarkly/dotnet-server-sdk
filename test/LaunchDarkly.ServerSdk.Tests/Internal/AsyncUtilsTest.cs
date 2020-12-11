using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Internal
{
    public class AsyncUtilsTest
    {
        [Fact]
        public async void MultiNotifierNotifiesWaitingTasks()
        {
            var notifier = new MultiNotifier();
            var task1Started = new TaskCompletionSource<bool>();
            var task2Started = new TaskCompletionSource<bool>();
            var task3Started = new TaskCompletionSource<bool>();
            var task1Done = new TaskCompletionSource<bool>();
            var task2Done = new TaskCompletionSource<bool>();
            var task3Done = new TaskCompletionSource<bool>();
            long doneCount = 0;

            async Task doTask(TaskCompletionSource<bool> setWhenStarted, TaskCompletionSource<bool> setWhenDone)
            {
                var token = notifier.Token;
                setWhenStarted.SetResult(true);
                if (await token.WaitAsync(TimeSpan.FromSeconds(10)))
                {
                    Interlocked.Increment(ref doneCount);
                }
                setWhenDone.SetResult(true);
            }

            var task1 = Task.Run(async () => await doTask(task1Started, task1Done));
            var task2 = Task.Run(async () => await doTask(task2Started, task2Done));
            
            await task1Started.Task;
            await task2Started.Task;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.Equal(0, Interlocked.Read(ref doneCount));

            notifier.NotifyAll();
            await task1Done.Task;
            await task2Done.Task;
            Assert.Equal(2, Interlocked.Read(ref doneCount));

            // Now it's been reset so a subsequent awaiter must wait for the next signal
            var task3 = Task.Run(async () => await doTask(task3Started, task3Done));
            await task3Started.Task;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.Equal(2, Interlocked.Read(ref doneCount));
            notifier.NotifyAll();
            await task3Done.Task;
            Assert.Equal(3, Interlocked.Read(ref doneCount));
        }

        [Fact]
        public async void MultiNotifierDoesNotLetAnyoneWaitAfterBeingDisposed()
        {
            var notifier = new MultiNotifier();
            notifier.Dispose();
            Assert.True(await notifier.Token.WaitAsync(TimeSpan.FromMinutes(30)));
        }
    }
}
