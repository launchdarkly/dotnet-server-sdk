using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Internal
{
    public class TaskExecutorTest : BaseTest
    {
        private readonly TaskExecutor executor;
        private event EventHandler<string> myEvent;

        public TaskExecutorTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            executor = new TaskExecutor(testLogger);
        }

        [Fact]
        public void SendsEvent()
        {
            var values1 = new EventSink<string>();
            var values2 = new EventSink<string>();
            myEvent += values1.Add;
            myEvent += values2.Add;

            executor.ScheduleEvent(this, "hello", myEvent);

            Assert.Equal("hello", values1.ExpectValue());
            Assert.Equal("hello", values2.ExpectValue());
        }

        [Fact]
        public void ExceptionFromEventHandlerIsLoggedAndDoesNotStopOtherHandlers()
        {
            var values1 = new EventSink<string>();
            myEvent += (sender, args) => throw new Exception("sorry");
            myEvent += values1.Add;

            executor.ScheduleEvent(this, "hello", myEvent);

            Assert.Equal("hello", values1.ExpectValue());

            AssertEventually(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(20), () =>
                logCapture.HasMessageWithText(LogLevel.Error, "Unexpected exception from event handler: System.Exception: sorry") &&
                logCapture.HasMessageWithRegex(LogLevel.Debug, "at LaunchDarkly.Sdk.Server.Internal.TaskExecutorTest"));
        }

        [Fact]
        public void RepeatingTask()
        {
            var values = new BlockingCollection<int>();
            var testGate = new EventWaitHandle(false, EventResetMode.AutoReset);
            var nextValue = 1;
            var canceller = executor.StartRepeatingTask(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), async () =>
            {
                testGate.WaitOne();
                values.Add(nextValue++);
                await Task.FromResult(true); // an arbitrary await just to make this function async
            });

            testGate.Set();
            Assert.True(values.TryTake(out var value1, TimeSpan.FromSeconds(2)));
            Assert.Equal(1, value1);

            testGate.Set();
            Assert.True(values.TryTake(out var value2, TimeSpan.FromSeconds(2)));
            Assert.Equal(2, value2);

            canceller.Cancel();
            testGate.Set();
            Assert.False(values.TryTake(out _, TimeSpan.FromMilliseconds(200)));
        }

        [Fact]
        public void ExceptionFromRepeatingTaskIsLoggedAndDoesNotStopTask()
        {
            var values = new BlockingCollection<int>();
            var testGate = new EventWaitHandle(false, EventResetMode.AutoReset);
            var nextValue = 1;
            var canceller = executor.StartRepeatingTask(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), async () =>
            {
                testGate.WaitOne();
                var valueWas = nextValue++;
                if (valueWas == 1)
                {
                    throw new Exception("sorry");
                }
                else
                {
                    values.Add(valueWas++);
                }
                await Task.FromResult(true); // an arbitrary await just to make this function async
            });

            testGate.Set();
            Assert.False(values.TryTake(out _, TimeSpan.FromMilliseconds(100)));

            AssertEventually(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(20), () =>
                logCapture.HasMessageWithText(LogLevel.Error, "Unexpected exception from repeating task: System.Exception: sorry") &&
                logCapture.HasMessageWithRegex(LogLevel.Debug, "at LaunchDarkly.Sdk.Server.Internal.TaskExecutorTest"));

            testGate.Set();
            Assert.True(values.TryTake(out var value2, TimeSpan.FromSeconds(2)));
            Assert.Equal(2, value2);

            canceller.Cancel();
            testGate.Set();
        }

        private static void AssertEventually(TimeSpan timeout, TimeSpan interval, Func<bool> test)
        {
            var deadline = DateTime.Now.Add(timeout);
            while (DateTime.Now < deadline)
            {
                if (test())
                {
                    return;
                }
                Thread.Sleep(interval);
            }
            Assert.True(false, "timed out before test condition was satisfied");
        }
    }
}
