using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Client.Interfaces;

namespace LaunchDarkly.Client.Utils
{
    /// <summary>
    /// Used internally by CachingStoreWrapper to call asynchronous IDataStoreCoreAsync
    /// methods from synchronous code. In the future, if the SDK internals are rewritten to
    /// use async/await, we may reverse this and instead put an adapter around synchronous
    /// methods.
    /// </summary>
    internal class DataStoreCoreAsyncAdapter : IDataStoreCore
    {
        private readonly IDataStoreCoreAsync _coreAsync;
        private static readonly TaskFactory _taskFactory = new TaskFactory(CancellationToken.None,
            TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

        internal DataStoreCoreAsyncAdapter(IDataStoreCoreAsync coreAsync)
        {
            _coreAsync = coreAsync;
        }

        public void InitInternal(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData)
        {
            WaitSafely(() => _coreAsync.InitInternalAsync(allData));
        }

        public IVersionedData GetInternal(IVersionedDataKind kind, string key)
        {
            return WaitSafely(() => _coreAsync.GetInternalAsync(kind, key));
        }

        public IDictionary<string, IVersionedData> GetAllInternal(IVersionedDataKind kind)
        {
            return WaitSafely(() => _coreAsync.GetAllInternalAsync(kind));
        }

        public IVersionedData UpsertInternal(IVersionedDataKind kind, IVersionedData item)
        {
            return WaitSafely(() => _coreAsync.UpsertInternalAsync(kind, item));
        }

        public bool InitializedInternal()
        {
            return WaitSafely(() => _coreAsync.InitializedInternalAsync());
        }

        public void Dispose()
        {
            _coreAsync.Dispose();
        }

        // This procedure for blocking on a Task without using Task.Wait is derived from the MIT-licensed ASP.NET
        // code here: https://github.com/aspnet/AspNetIdentity/blob/master/src/Microsoft.AspNet.Identity.Core/AsyncHelper.cs
        // In general, mixing sync and async code is not recommended, and if done in other ways can result in
        // deadlocks. See: https://stackoverflow.com/questions/9343594/how-to-call-asynchronous-method-from-synchronous-method-in-c
        // Task.Wait would only be safe if we could guarantee that every intermediate Task within the async
        // code had been modified with ConfigureAwait(false), but that is very error-prone and we can't depend
        // on data store implementors doing so.

        private void WaitSafely(Func<Task> taskFn)
        {
            _taskFactory.StartNew(taskFn)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
        }

        private T WaitSafely<T>(Func<Task<T>> taskFn)
        {
            return _taskFactory.StartNew(taskFn)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
        }
    }
}
