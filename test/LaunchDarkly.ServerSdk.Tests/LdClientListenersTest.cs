using System;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientListenersTest : BaseTest
    {
        public LdClientListenersTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void ClientSendsFlagChangeEvents()
        {
            var flagKey = "flagKey";
            var testData = TestData.DataSource();
            testData.Update(testData.Flag(flagKey).On(true));
            var config = Configuration.Builder("").DataSource(testData)
                .Events(Components.NoEvents).Build();

            using (var client = new LdClient(config))
            {
                var eventSink1 = new EventSink<FlagChangeEvent>();
                var eventSink2 = new EventSink<FlagChangeEvent>();
                EventHandler<FlagChangeEvent> listener1 = eventSink1.Add;
                EventHandler<FlagChangeEvent> listener2 = eventSink2.Add;
                client.FlagTracker.FlagChanged += listener1;
                client.FlagTracker.FlagChanged += listener2;

                eventSink1.ExpectNoValue();
                eventSink2.ExpectNoValue();

                testData.Update(testData.Flag(flagKey).On(false));

                var event1 = eventSink1.ExpectValue();
                var event2 = eventSink2.ExpectValue();
                Assert.Equal(flagKey, event1.Key);
                Assert.Equal(flagKey, event2.Key);

                eventSink1.ExpectNoValue();
                eventSink2.ExpectNoValue();

                client.FlagTracker.FlagChanged -= listener2;

                testData.Update(testData.Flag(flagKey).On(true));

                var event3 = eventSink1.ExpectValue();
                Assert.Equal(flagKey, event3.Key);
                eventSink2.ExpectNoValue();
            }
        }

        [Fact]
        public void ClientSendsFlagValueChangeEvents()
        {
            var flagKey = "flagKey";
            var user = User.WithKey("important-user");
            var otherUser = User.WithKey("unimportant-user");

            var testData = TestData.DataSource();
            testData.Update(testData.Flag(flagKey).On(false));

            var config = Configuration.Builder("").DataSource(testData)
                .Events(Components.NoEvents).Build();

            using (var client = new LdClient(config))
            {
                var eventSink1 = new EventSink<FlagValueChangeEvent>();
                var eventSink2 = new EventSink<FlagValueChangeEvent>();
                var eventSink3 = new EventSink<FlagValueChangeEvent>();
                var listener1 = client.FlagTracker.FlagValueChangeHandler(flagKey, user, eventSink1.Add);
                var listener2 = client.FlagTracker.FlagValueChangeHandler(flagKey, user, eventSink2.Add);
                var listener3 = client.FlagTracker.FlagValueChangeHandler(flagKey, otherUser, eventSink3.Add);
                client.FlagTracker.FlagChanged += listener1;
                client.FlagTracker.FlagChanged += listener2;
                client.FlagTracker.FlagChanged -= listener2; // just verifying that removing a listener works
                client.FlagTracker.FlagChanged += listener3;

                eventSink1.ExpectNoValue();
                eventSink2.ExpectNoValue();
                eventSink3.ExpectNoValue();

                // make the flag true for the first user only, and broadcast a flag change event
                testData.Update(testData.Flag(flagKey)
                    .On(true)
                    .VariationForUser(user.Key, true)
                    .FallthroughVariation(false));

                // eventSink1 receives a value change event
                var event1 = eventSink1.ExpectValue();
                Assert.Equal(flagKey, event1.Key);
                Assert.Equal(LdValue.Of(false), event1.OldValue);
                Assert.Equal(LdValue.Of(true), event1.NewValue);
                eventSink1.ExpectNoValue();

                // eventSink2 doesn't receive one, because it was unregistered
                eventSink2.ExpectNoValue();

                // eventSink3 doesn't receive one, because the flag's value hasn't changed for otherUser
                eventSink3.ExpectNoValue();
            }
        }

        [Fact]
        public void DataSourceStatusProviderReturnsLatestStatus()
        {
            var testData = TestData.DataSource();
            var config = Configuration.Builder("")
                .DataSource(testData)
                .Events(Components.NoEvents)
                .Logging(Components.Logging(testLogging))
                .Build();
            var timeBeforeStarting = DateTime.Now;

            using (var client = new LdClient(config))
            {
                var initialStatus = client.DataSourceStatusProvider.Status;
                Assert.Equal(DataSourceState.Valid, initialStatus.State);
                Assert.True(initialStatus.StateSince >= timeBeforeStarting);
                Assert.Null(initialStatus.LastError);

                var errorInfo = DataSourceStatus.ErrorInfo.FromHttpError(401);
                testData.UpdateStatus(DataSourceState.Off, errorInfo);

                var newStatus = client.DataSourceStatusProvider.Status;
                Assert.Equal(DataSourceState.Off, newStatus.State);
                Assert.NotEqual(initialStatus.StateSince, newStatus.StateSince);
                Assert.True(newStatus.StateSince >= errorInfo.Time);
                Assert.Equal(errorInfo, newStatus.LastError);
            }
        }

        [Fact]
        public void DataSourceStatusProviderSendsStatusUpdates()
        {
            var testData = TestData.DataSource();
            var config = Configuration.Builder("")
                .DataSource(testData)
                .Logging(Components.Logging(testLogging))
                .Events(Components.NoEvents).Build();
            
            using (var client = new LdClient(config))
            {
                var statuses = new EventSink<DataSourceStatus>();
                client.DataSourceStatusProvider.StatusChanged += statuses.Add;

                var errorInfo = DataSourceStatus.ErrorInfo.FromHttpError(401);
                testData.UpdateStatus(DataSourceState.Off, errorInfo);

                var newStatus = statuses.ExpectValue();
                Assert.Equal(DataSourceState.Off, newStatus.State);
                Assert.True(newStatus.StateSince >= errorInfo.Time);
                Assert.Equal(errorInfo, newStatus.LastError);
            }
        }

        [Fact]
        public void DataStoreStatusMonitoringIsDisabledForInMemoryDataStore()
        {
            var config = Configuration.Builder("")
                .DataSource(Components.ExternalUpdatesOnly)
                .Events(Components.NoEvents)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.False(client.DataStoreStatusProvider.StatusMonitoringEnabled);
            }
        }

        [Fact]
        public void DataStoreStatusMonitoringIsEnabledForPersistentStore()
        {
            var config = Configuration.Builder("")
                .DataSource(Components.ExternalUpdatesOnly)
                .DataStore(Components.PersistentDataStore(TestUtils.ArbitraryPersistentDataStore))
                .Events(Components.NoEvents)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.True(client.DataStoreStatusProvider.StatusMonitoringEnabled);
            }
        }

        [Fact]
        public void DataStoreStatusProviderReturnsLatestStatus()
        {
            var dataStoreFactory = new CapturingDataStoreFactory(Components.InMemoryDataStore);
            var config = Configuration.Builder("")
                .DataSource(Components.ExternalUpdatesOnly)
                .DataStore(dataStoreFactory)
                .Events(Components.NoEvents)
                .Build();

            using (var client = new LdClient(config))
            {
                Assert.Equal(new DataStoreStatus { Available = true },
                    client.DataStoreStatusProvider.Status);

                var newStatus = new DataStoreStatus { Available = false };
                dataStoreFactory.DataStoreUpdates.UpdateStatus(newStatus);

                Assert.Equal(newStatus, client.DataStoreStatusProvider.Status);
            }
        }

        [Fact]
        public void DataStoreStatusProviderSendsStatusUpdates()
        {
            var dataStoreFactory = new CapturingDataStoreFactory(Components.InMemoryDataStore);
            var config = Configuration.Builder("")
                .DataSource(Components.ExternalUpdatesOnly)
                .DataStore(dataStoreFactory)
                .Events(Components.NoEvents)
                .Build();

            using (var client = new LdClient(config))
            {
                var statuses = new EventSink<DataStoreStatus>();
                client.DataStoreStatusProvider.StatusChanged += statuses.Add;

                var newStatus = new DataStoreStatus { Available = false };
                dataStoreFactory.DataStoreUpdates.UpdateStatus(newStatus);

                Assert.Equal(newStatus, statuses.ExpectValue());
            }
        }
    }
}
