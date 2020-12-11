using System;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.Sdk.Server.Internal
{
    internal static class AsyncUtils
    {
        private static readonly TaskFactory _taskFactory = new TaskFactory(CancellationToken.None,
            TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

        // This procedure for blocking on a Task without using Task.Wait is derived from the MIT-licensed ASP.NET
        // code here: https://github.com/aspnet/AspNetIdentity/blob/master/src/Microsoft.AspNet.Identity.Core/AsyncHelper.cs
        // In general, mixing sync and async code is not recommended, and if done in other ways can result in
        // deadlocks. See: https://stackoverflow.com/questions/9343594/how-to-call-asynchronous-method-from-synchronous-method-in-c
        // Task.Wait would only be safe if we could guarantee that every intermediate Task within the async
        // code had been modified with ConfigureAwait(false), but that is very error-prone and we can't depend
        // on feature store implementors doing so.

        internal static void WaitSafely(Func<Task> taskFn)
        {
            _taskFactory.StartNew(taskFn)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
            // Note, GetResult does not throw AggregateException so we don't need to post-process exceptions
        }

        internal static bool WaitSafely(Func<Task> taskFn, TimeSpan timeout)
        {
            try
            {
                return _taskFactory.StartNew(taskFn)
                    .Unwrap()
                    .Wait(timeout);
            }
            catch (AggregateException e)
            {
                throw UnwrapAggregateException(e);
            }
        }

        internal static T WaitSafely<T>(Func<Task<T>> taskFn)
        {
            return _taskFactory.StartNew(taskFn)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
        }

        private static Exception UnwrapAggregateException(AggregateException e)
        {
            if (e.InnerExceptions.Count == 1)
            {
                return e.InnerExceptions[0];
            }
            return e;
        }
    }

    /// <summary>
    /// Provides the equivalent of Java's wait/notifyAll concurrency primitives for async code.
    /// </summary>
    /// <remarks>
    /// Any number of tasks can call WaitAsync on a MultiNotifierToken from the same MultiNotifier.
    /// If another task calls NotifyAll, all of the tasks that were waiting up to that point will be
    /// signaled at once and stop waiting. Having MultiNotifierToken be a separate object allows you
    /// to use this safely in conjunction with a shared resource under a lock, as shown in the
    /// example code.
    /// </remarks>
    /// <example>
    /// <code>
    ///     int sharedValue = 0;
    ///     object sharedValueMutex = new object();
    ///     MultiNotifier sharedValueSignal = new MultiNotifier();
    ///
    ///     async void WaitUntilSharedValueIsAtLeastThree()
    ///     {
    ///         MultiNotifierToken token;
    ///         lock (sharedValueMutex)
    ///         {
    ///             if (sharedValue >= 3)
    ///             {
    ///                 return;
    ///             }
    ///             token = sharedValueSignal.Token;
    ///             // By acquiring the token while we're holding the lock, we avoid a race
    ///             // condition where IncrementSharedValue might notify and reset the signal
    ///             // before we start waiting on it.
    ///         }
    ///         token.WaitAsync();
    ///     }
    ///
    ///     void IncrementSharedValue()
    ///     {
    ///         lock (sharedValueMutex)
    ///         {
    ///             sharedValue++;
    ///             sharedValueSignal.NotifyAll();
    ///         }
    ///     }
    /// </code>
    /// </example>
    internal sealed class MultiNotifier : IDisposable
    {
        private readonly object _lock = new object();
        private CancellationTokenSource _canceller = new CancellationTokenSource();

        /// <summary>
        /// Returns a token for waiting on this MultiNotifier.
        /// </summary>
        /// <remarks>
        /// The reason that this is a separate step, instead of having WaitForAsync be a method on
        /// MultiNotifier, is to avoid race conditions where the consumer has to (1) check for some
        /// state that determines whether it will need to wait on the MultiNotifier, and then (2)
        /// wait on the signal, but the owner of the MultiNotifier calls NotifyAll on it in between
        /// (1) and (2), causing it to be reset so the consumer is left waiting for a <i>second</i>
        /// signal in (2). By acquiring the Token before (1) (or atomically with it, under a lock),
        /// the consumer ensures that any NotifyAll that happens after that point will affect that
        /// Token.
        /// </remarks>
        public MultiNotifierToken Token
        {
            get
            {
                lock(_lock)
                {
                    return new MultiNotifierToken(_canceller is null ? null :
                        (CancellationToken?)_canceller.Token);
                }
            }
        }

        /// <summary>
        /// Notifies any tasks that have been waiting on this MultiNotifier that they should stop
        /// waiting, and resets the state so that any further calls to WaitAsync after this point
        /// will wait until the <i>next</i> NotifyAll.
        /// </summary>
        public void NotifyAll()
        {
            lock (_lock)
            {
                if (_canceller != null)
                {
                    _canceller.Cancel();
                    _canceller = new CancellationTokenSource();
                }
            }
        }

        /// <summary>
        /// Disposing of the MultiNotifier notifies any current awaiters, and causes all subsequent
        /// uses of <see cref="Token"/> to produce a stub that will never wait.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_canceller != null)
                {
                    _canceller.Cancel();
                    _canceller = null;
                }
            }
        }
    }

    /// <summary>
    /// Used in conjunction with <see cref="MultiNotifier"/>.
    /// </summary>
    internal struct MultiNotifierToken
    {
        private readonly CancellationToken? _token;

        internal MultiNotifierToken(CancellationToken? token)
        {
            _token = token;
        }

        /// <summary>
        /// Waits until another task calls <see cref="MultiNotifier.NotifyAll"/> on the parent
        /// <see cref="MultiNotifier"/>, or until the timeout elapses, whichever comes first.
        /// </summary>
        /// <param name="timeout">the timeout</param>
        /// <returns>true if <see cref="MultiNotifier.NotifyAll"/> was called, false if it timed
        /// out first</returns>
        public async Task<bool> WaitAsync(TimeSpan timeout)
        {
            if (!_token.HasValue)
            {
                return true;
            }
            try
            {
                await Task.Delay(timeout, _token.Value);

                // If Task.Delay returned normally without an exception, it means the CancellationToken
                // was never fired - MultiNotifier.Notify was not called. So we timed out.
                return false;
            }
            catch (TaskCanceledException)
            {
                return true;
            }
        }
    }
}
