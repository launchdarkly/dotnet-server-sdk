using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Subsystems;
using LaunchDarkly.TestHelpers;

using static LaunchDarkly.Sdk.Server.Subsystems.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;

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
        public static IComponentConfigurer<T> AsSingletonFactory<T>(this T instance) =>
            new SingleComponentFactory<T> { Instance = instance };

        public static IComponentConfigurer<T> AsSingletonFactoryWithDiagnosticDescription<T>(this T instance, LdValue description) =>
            new SingleComponentFactoryWithDiagnosticDescription<T> { Instance = instance, Description = description };

        private class SingleComponentFactory<T> : IComponentConfigurer<T>
        {
            public T Instance { get; set; }
            public T Build(LdClientContext context) => Instance;
        }

        private class SingleComponentFactoryWithDiagnosticDescription<T> : SingleComponentFactory<T>, IDiagnosticDescription
        {
            public LdValue Description { get; set; }
            public LdValue DescribeConfiguration(LdClientContext context) => Description;
        }
    }

    public class CapturingDataSourceUpdates : IDataSourceUpdates
    {
        internal readonly EventSink<FullDataSet<ItemDescriptor>> Inits =
            new EventSink<FullDataSet<ItemDescriptor>>();
        internal readonly EventSink<UpsertParams> Upserts = new EventSink<UpsertParams>();
        internal readonly EventSink<DataSourceStatus> StatusUpdates = new EventSink<DataSourceStatus>();

        public struct UpsertParams
        {
            public DataKind Kind;
            public string Key;
            public ItemDescriptor Item;
        }

        internal MockDataStoreStatusProvider MockDataStoreStatusProvider = new MockDataStoreStatusProvider();

        internal int InitsShouldFail = 0;

        internal int UpsertsShouldFail = 0;

        public IDataStoreStatusProvider DataStoreStatusProvider => MockDataStoreStatusProvider;

        public bool Init(FullDataSet<ItemDescriptor> allData)
        {
            Inits.Enqueue(allData);
            return InitsShouldFail <= 0 || (--InitsShouldFail < 0);
        }

        public void UpdateStatus(DataSourceState newState, DataSourceStatus.ErrorInfo? newError) =>
            StatusUpdates.Enqueue(new DataSourceStatus() { State = newState, LastError = newError });

        public bool Upsert(DataKind kind, string key, ItemDescriptor item)
        {
            Upserts.Enqueue(new UpsertParams { Kind = kind, Key = key, Item = item });
            return UpsertsShouldFail <= 0 || (--UpsertsShouldFail < 0);
        }
    }

    public class CapturingDataStoreFactory : IComponentConfigurer<IDataStore>
    {
        private readonly IComponentConfigurer<IDataStore> _factory;
        public volatile LdClientContext Context;
        public volatile IDataStoreUpdates DataStoreUpdates;

        public CapturingDataStoreFactory(IComponentConfigurer<IDataStore> factory)
        {
            _factory = factory;
        }

        public IDataStore Build(LdClientContext context)
        {
            Context = context;
            DataStoreUpdates = context.DataStoreUpdates;
            return _factory.Build(context);
        }
    }

    public static class MockComponents
    {
        internal static MockDataSource MockDataSourceThatNeverStarts() =>
            MockDataSourceWithStartFn(_ => new TaskCompletionSource<bool>().Task, () => false);

        internal static MockDataSource MockDataSourceWithData(FullDataSet<ItemDescriptor> data) =>
            MockDataSourceWithStartFn(updateSink =>
            {
                updateSink.Init(data);
                return Task.FromResult(true);
            });

        internal static MockDataSource MockDataSourceWithStartFn(Func<IDataSourceUpdates, Task<bool>> startFn) =>
            new MockDataSource(startFn, () => true);

        internal static MockDataSource MockDataSourceWithStartFn(Func<IDataSourceUpdates, Task<bool>> startFn,
            Func<bool> initedFn) => new MockDataSource(startFn, initedFn);

        internal sealed class MockDataSource : MockDataSourceBase, IDataSource, IComponentConfigurer<IDataSource>
        {
            private readonly Func<IDataSourceUpdates, Task<bool>> _startFn;
            private readonly Func<bool> _initedFn;

            internal MockDataSource(Func<IDataSourceUpdates, Task<bool>> startFn, Func<bool> initedFn)
            {
                _startFn = startFn;
                _initedFn = initedFn;
            }

            public Task<bool> Start() => _startFn(UpdateSink);

            public bool Initialized => _initedFn();

            public void Dispose() { }

            public IDataSource Build(LdClientContext context)
            {
                UpdateSink = context.DataSourceUpdates;
                return this;
            }
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

    public sealed class MockDataStoreStatusProvider : IDataStoreStatusProvider
    {
        public DataStoreStatus Status { get; set; } = new DataStoreStatus { Available = true };

        public bool StatusMonitoringEnabled { get; set; } = false;

        public event EventHandler<DataStoreStatus> StatusChanged;

        public void FireStatusChanged(DataStoreStatus status)
        {
            Status = status;
            StatusChanged?.Invoke(this, status);
        }
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

        public Task<EventSenderResult> SendEventDataAsync(EventDataKind kind, byte[] data, int eventCount)
        {
            if (!FilterKind.HasValue || kind == FilterKind.Value)
            {
                Calls.Add(new Params { Kind = kind, Data = Encoding.UTF8.GetString(data), EventCount = eventCount });
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
