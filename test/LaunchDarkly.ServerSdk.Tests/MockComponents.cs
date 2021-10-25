using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.TestHelpers;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server
{
    // In some cases, we can use reflective mocking via Moq to set up test conditions. However, that's
    // not always flexible enough for the kind of test state management we want, and also Moq does not
    // always handle concurrency well (i.e. changing a mock object's state when it is being used by
    // another thread). So this file contains instrumented test versions of many component interfaces.

    public static class MockComponentExtensions
    {
        // Normally SDK configuration always specifies component factories rather than component instances,
        // so that the SDK can handle the component lifecycle and dependency injection. However, in tests,
        // we often want to set up a specific component instance; .AsSingletonFactory() wraps it in a
        // factory that always returns that instance.
        public static IBigSegmentStoreFactory AsSingletonFactory(this IBigSegmentStore instance) =>
            new SingleBigSegmentStoreFactory { Instance = instance };

        public static IDataSourceFactory AsSingletonFactory(this IDataSource instance) =>
            new SingleDataSourceFactory { Instance = instance };

        public static IDataStoreFactory AsSingletonFactory(this IDataStore instance) =>
            new SingleDataStoreFactory { Instance = instance };

        public static IEventProcessorFactory AsSingletonFactory(this IEventProcessor instance) =>
            new SingleEventProcessorFactory { Instance = instance };

        public static IPersistentDataStoreFactory AsSingletonFactory(this IPersistentDataStore instance) =>
            new SinglePersistentDataStoreFactory { Instance = instance };

        private class SingleBigSegmentStoreFactory : IBigSegmentStoreFactory
        {
            public IBigSegmentStore Instance { get; set; }
            public IBigSegmentStore CreateBigSegmentStore(LdClientContext context) => Instance;
        }

        private class SingleDataSourceFactory : IDataSourceFactory
        {
            public IDataSource Instance { get; set; }
            public IDataSource CreateDataSource(LdClientContext context, IDataSourceUpdates updateSink)
            {
                if (Instance is MockDataSourceBase m)
                {
                    m.UpdateSink = updateSink;
                }
                return Instance;
            }
        }

        private class SingleDataStoreFactory : IDataStoreFactory
        {
            public IDataStore Instance { get; set; }
            public IDataStore CreateDataStore(LdClientContext context, IDataStoreUpdates updateSink) => Instance;
        }

        private class SingleEventProcessorFactory : IEventProcessorFactory
        {
            public IEventProcessor Instance { get; set; }
            public IEventProcessor CreateEventProcessor(LdClientContext context) => Instance;
        }

        private class SinglePersistentDataStoreFactory : IPersistentDataStoreFactory
        {
            public IPersistentDataStore Instance { get; set; }
            public IPersistentDataStore CreatePersistentDataStore(LdClientContext context) => Instance;
        }
    }

    public class CapturingDataSourceUpdates : IDataSourceUpdates
    {
        public readonly EventSink<FullDataSet<ItemDescriptor>> Inits =
            new EventSink<FullDataSet<ItemDescriptor>>();
        public readonly EventSink<UpsertParams> Upserts = new EventSink<UpsertParams>();
        public readonly EventSink<StatusParams> StatusUpdates = new EventSink<StatusParams>();

        public struct StatusParams
        {
            public DataSourceState State;
            public DataSourceStatus.ErrorInfo? Error;
        }

        public struct UpsertParams
        {
            public DataKind Kind;
            public string Key;
            public ItemDescriptor Item;
        }

        public DataSourceState State;

        public IDataStoreStatusProvider DataStoreStatusProvider => throw new NotImplementedException();

        public bool Init(FullDataSet<ItemDescriptor> allData)
        {
            Inits.Enqueue(allData);
            return true;
        }

        public void UpdateStatus(DataSourceState newState, DataSourceStatus.ErrorInfo? newError) =>
            StatusUpdates.Enqueue(new StatusParams { State = newState, Error = newError });

        public bool Upsert(DataKind kind, string key, ItemDescriptor item)
        {
            Upserts.Enqueue(new UpsertParams { Kind = kind, Key = key, Item = item });
            return true;
        }
    }

    public class CapturingDataStoreFactory : IDataStoreFactory
    {
        private readonly IDataStoreFactory _factory;
        public volatile LdClientContext Context;
        public volatile IDataStoreUpdates DataStoreUpdates;

        public CapturingDataStoreFactory(IDataStoreFactory factory)
        {
            _factory = factory;
        }

        public IDataStore CreateDataStore(LdClientContext context, IDataStoreUpdates dataStoreUpdates)
        {
            Context = context;
            DataStoreUpdates = dataStoreUpdates;
            return _factory.CreateDataStore(context, dataStoreUpdates);
        }
    }

    public static class MockComponents
    {
        internal static IDataSource MockDataSourceThatNeverStarts() =>
            MockDataSourceWithStartFn(_ => new TaskCompletionSource<bool>().Task, () => false);

        internal static IDataSource MockDataSourceWithData(FullDataSet<ItemDescriptor> data) =>
            MockDataSourceWithStartFn(updateSink =>
            {
                updateSink.Init(data);
                return Task.FromResult(true);
            });

        internal static IDataSource MockDataSourceWithStartFn(Func<IDataSourceUpdates, Task<bool>> startFn) =>
            new MockDataSourceWithStartFnImpl(startFn, () => true);

        internal static IDataSource MockDataSourceWithStartFn(Func<IDataSourceUpdates, Task<bool>> startFn,
            Func<bool> initedFn) => new MockDataSourceWithStartFnImpl(startFn, initedFn);

        private sealed class MockDataSourceWithStartFnImpl : MockDataSourceBase, IDataSource
        {
            private readonly Func<IDataSourceUpdates, Task<bool>> _startFn;
            private readonly Func<bool> _initedFn;

            internal MockDataSourceWithStartFnImpl(Func<IDataSourceUpdates, Task<bool>> startFn, Func<bool> initedFn)
            {
                _startFn = startFn;
                _initedFn = initedFn;
            }

            public Task<bool> Start() => _startFn(UpdateSink);

            public bool Initialized => _initedFn();

            public void Dispose() { }
        }
    }

    public sealed class MockBigSegmentStore : IBigSegmentStore
    {
        private static readonly object _lock = new object();

        private StoreMetadata? _metadata;
        private Exception _metadataError;
        private Dictionary<string, IMembership> _memberships = new Dictionary<string, IMembership>();
        private Dictionary<string, Exception> _membershipErrors = new Dictionary<string, Exception>();
        private Dictionary<string, int> _membershipsQueried = new Dictionary<string, int>();

        public void Dispose() { }

        internal void SetupMetadataReturns(StoreMetadata? value)
        {
            lock (_lock)
            {
                _metadata = value;
                _metadataError = null;
            }
        }

        internal void SetupMetadataThrows(Exception e)
        {
            lock (_lock)
            {
                _metadataError = e;
            }
        }

        internal void SetupMembershipReturns(string userHash, IMembership value)
        {
            lock (_lock)
            {
                _memberships[userHash] = value;
            }
        }

        internal void SetupMembershipThrows(string userHash, Exception e)
        {
            lock (_lock)
            {
                _membershipErrors[userHash] = e;
            }
        }

        internal int InspectMembershipQueriedCount(string userHash)
        {
            lock (_lock)
            {
                return _membershipsQueried.TryGetValue(userHash, out var value) ? value : 0;
            }
        }

#pragma warning disable CS1998
        public async Task<IMembership> GetMembershipAsync(string userHash)
        {
            lock (_lock)
            {
                _membershipsQueried[userHash] =
                    (_membershipsQueried.TryGetValue(userHash, out var count) ? count : 0) + 1;
                if (_membershipErrors.TryGetValue(userHash, out var e))
                {
                    throw e;
                }
                return _memberships.TryGetValue(userHash, out var value) ? value : null;
            }
        }

        public async Task<StoreMetadata?> GetMetadataAsync()
        {
            lock (_lock)
            {
                if (_metadataError != null)
                {
                    throw _metadataError;
                }
                return _metadata;
            }
        }
#pragma warning restore CS1998
    }

    internal abstract class MockDataSourceBase
    {
        internal IDataSourceUpdates UpdateSink { get; set; }
    }

    public class MockEventProcessor : IEventProcessor
    {
        public List<object> Events = new List<object>();

        public void SetOffline(bool offline) { }

        public void Flush() { }

        public void Dispose() { }

        public void RecordEvaluationEvent(EventProcessorTypes.EvaluationEvent e) =>
            Events.Add(e);

        public void RecordIdentifyEvent(EventProcessorTypes.IdentifyEvent e) =>
            Events.Add(e);

        public void RecordCustomEvent(EventProcessorTypes.CustomEvent e) =>
            Events.Add(e);

        public void RecordAliasEvent(EventProcessorTypes.AliasEvent e) =>
            Events.Add(e);
    }

    public class MockEventSender : IEventSender
    {
        public BlockingCollection<Params> Calls = new BlockingCollection<Params>();
        public EventDataKind? FilterKind = null;

        public void Dispose() { }

        public struct Params
        {
            public EventDataKind Kind;
            public string Data;
            public int EventCount;
        }

        public Task<EventSenderResult> SendEventDataAsync(EventDataKind kind, string data, int eventCount)
        {
            if (!FilterKind.HasValue || kind == FilterKind.Value)
            {
                Calls.Add(new Params { Kind = kind, Data = data, EventCount = eventCount });
            }
            return Task.FromResult(new EventSenderResult(DeliveryStatus.Succeeded, null));
        }

        public Params RequirePayload()
        {
            Params result;
            if (!Calls.TryTake(out result, TimeSpan.FromSeconds(5)))
            {
                throw new System.Exception("did not receive an event payload");
            }
            return result;
        }

        public void RequireNoPayloadSent(TimeSpan timeout)
        {
            Params result;
            if (Calls.TryTake(out result, timeout))
            {
                throw new System.Exception("received an unexpected event payload");
            }
        }
    }
}
