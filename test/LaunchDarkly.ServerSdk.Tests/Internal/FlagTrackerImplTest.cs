using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataSources;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.TestUtils;

namespace LaunchDarkly.Sdk.Server.Internal
{
    public class FlagTrackerImplTest : BaseTest
    {
        // These tests only cover simple flag updates, not prerequisite/segment dependencies, because
        // the latter are covered in detail in DataSourceUpdatesImplTest; DataSourceUpdatesImpl is
        // where all of that behavior is actually implemented, FlagTracker is just a facade for that.

        public FlagTrackerImplTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void FlagChangeListeners()
        {
            var flagKey = "flagKey";
            var store = new InMemoryDataStore();
            var dataSourceUpdates = new DataSourceUpdatesImpl(store,
                new TaskExecutor(testLogger), testLogger, null);

            var tracker = new FlagTrackerImpl(dataSourceUpdates, null);

            var eventSink1 = new EventSink<FlagChangeEvent>();
            var eventSink2 = new EventSink<FlagChangeEvent>();
            EventHandler<FlagChangeEvent> listener1 = eventSink1.Add;
            EventHandler<FlagChangeEvent> listener2 = eventSink2.Add;
            tracker.FlagChanged += listener1;
            tracker.FlagChanged += listener2;

            eventSink1.ExpectNoValue();
            eventSink2.ExpectNoValue();

            var flagV1 = new FeatureFlagBuilder(flagKey).Version(1).Build();
            dataSourceUpdates.Upsert(DataKinds.Features, flagKey, DescriptorOf(flagV1));

            var event1 = eventSink1.ExpectValue();
            var event2 = eventSink2.ExpectValue();
            Assert.Equal(flagKey, event1.Key);
            Assert.Equal(flagKey, event2.Key);

            eventSink1.ExpectNoValue();
            eventSink2.ExpectNoValue();

            tracker.FlagChanged -= listener2;

            var flagV2 = new FeatureFlagBuilder(flagKey).Version(2).Build();
            dataSourceUpdates.Upsert(DataKinds.Features, flagKey, DescriptorOf(flagV2));

            var event3 = eventSink1.ExpectValue();
            Assert.Equal(flagKey, event3.Key);
            eventSink2.ExpectNoValue();
        }

        [Fact]
        public void FlagValueChangeListener()
        {
            var flagKey = "important-flag";
            var user = User.WithKey("important-user");
            var otherUser = User.WithKey("unimportant-user");
            var store = new InMemoryDataStore();
            var dataSourceUpdates = new DataSourceUpdatesImpl(store,
                new TaskExecutor(testLogger), testLogger, null);

            var resultMap = new Dictionary<KeyValuePair<string, User>, LdValue>();
            
            var tracker = new FlagTrackerImpl(dataSourceUpdates, (key, u) =>
                resultMap[new KeyValuePair<string, User>(key, u)]);

            resultMap[new KeyValuePair<string, User>(flagKey, user)] = LdValue.Of(false);
            resultMap[new KeyValuePair<string, User>(flagKey, otherUser)] = LdValue.Of(false);

            var eventSink1 = new EventSink<FlagValueChangeEvent>();
            var eventSink2 = new EventSink<FlagValueChangeEvent>();
            var eventSink3 = new EventSink<FlagValueChangeEvent>();
            var listener1 = tracker.FlagValueChangeHandler(flagKey, user, eventSink1.Add);
            var listener2 = tracker.FlagValueChangeHandler(flagKey, user, eventSink2.Add);
            var listener3 = tracker.FlagValueChangeHandler(flagKey, otherUser, eventSink3.Add);
            tracker.FlagChanged += listener1;
            tracker.FlagChanged += listener2;
            tracker.FlagChanged -= listener2; // just verifying that removing a listener works
            tracker.FlagChanged += listener3;

            eventSink1.ExpectNoValue();
            eventSink2.ExpectNoValue();
            eventSink3.ExpectNoValue();

            // make the flag true for the first user only, and broadcast a flag change event
            resultMap[new KeyValuePair<string, User>(flagKey, user)] = LdValue.Of(true);
            var flagV1 = new FeatureFlagBuilder(flagKey).Version(1).Build();
            dataSourceUpdates.Upsert(DataKinds.Features, flagKey, DescriptorOf(flagV1));

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
}
