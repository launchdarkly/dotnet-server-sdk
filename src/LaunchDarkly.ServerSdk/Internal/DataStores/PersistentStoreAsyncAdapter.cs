using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    /// <summary>
    /// Used internally by PersistentStoreWrapper to call asynchronous IPersistentDataStoreAsync
    /// methods from synchronous code. In the future, if the SDK internals are rewritten to
    /// use async/await, we may reverse this and instead put an adapter around synchronous
    /// methods.
    /// </summary>
    internal class PersistentStoreAsyncAdapter : IPersistentDataStore
    {
        private readonly IPersistentDataStoreAsync _coreAsync;
        private static readonly TaskFactory _taskFactory = new TaskFactory(CancellationToken.None,
            TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

        internal PersistentStoreAsyncAdapter(IPersistentDataStoreAsync coreAsync)
        {
            _coreAsync = coreAsync;
        }

        public void Init(FullDataSet<SerializedItemDescriptor> allData)
        {
            WaitSafely(() => _coreAsync.InitAsync(allData));
        }
        
        public SerializedItemDescriptor? Get(DataKind kind, string key)
        {
            return WaitSafely(() => _coreAsync.GetAsync(kind, key));
        }
        
        public KeyedItems<SerializedItemDescriptor> GetAll(DataKind kind)
        {
            return WaitSafely(() => _coreAsync.GetAllAsync(kind));
        }
        
        public bool Upsert(DataKind kind, string key, SerializedItemDescriptor item)
        {
            return WaitSafely(() => _coreAsync.UpsertAsync(kind, key, item));
        }
        
        public bool Initialized()
        {
            return WaitSafely(() => _coreAsync.InitializedAsync());
        }

        public bool IsStoreAvailable()
        {
            return WaitSafely(() => _coreAsync.IsStoreAvailableAsync());
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
