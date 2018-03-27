using System.Collections.Generic;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class EventSummarizerTest
    {
        private User _user = new User("key");
        private TestEventFactory _eventFactory = new TestEventFactory();

        [Fact]
        public void SummarizeEventDoesNothingForIdentifyEvent()
        {
            EventSummarizer es = new EventSummarizer();
            SummaryState snapshot = es.Snapshot();
            es.SummarizeEvent(_eventFactory.NewIdentifyEvent(_user));
            SummaryState snapshot2 = es.Snapshot();
            Assert.Equal(snapshot.StartDate, snapshot2.StartDate);
            Assert.Equal(snapshot.EndDate, snapshot2.EndDate);
            Assert.Equal(snapshot.Counters, snapshot2.Counters);
        }

        [Fact]
        public void SummarizeEventDoesNothingForCustomEvent()
        {
            EventSummarizer es = new EventSummarizer();
            SummaryState snapshot = es.Snapshot();
            es.SummarizeEvent(_eventFactory.NewCustomEvent("whatever", _user, null));
            SummaryState snapshot2 = es.Snapshot();
            Assert.Equal(snapshot.StartDate, snapshot2.StartDate);
            Assert.Equal(snapshot.EndDate, snapshot2.EndDate);
            Assert.Equal(snapshot.Counters, snapshot2.Counters);
        }

        [Fact]
        public void SummarizeEventSetsStartAndEndDates()
        {
            EventSummarizer es = new EventSummarizer();
            FeatureFlag flag = new FeatureFlagBuilder("key").Build();
            _eventFactory.Timestamp = 2000;
            Event event1 = _eventFactory.NewFeatureRequestEvent(flag, _user, null, null, null);
            _eventFactory.Timestamp = 1000;
            Event event2 = _eventFactory.NewFeatureRequestEvent(flag, _user, null, null, null);
            _eventFactory.Timestamp = 1500;
            Event event3 = _eventFactory.NewFeatureRequestEvent(flag, _user, null, null, null);
            es.SummarizeEvent(event1);
            es.SummarizeEvent(event2);
            es.SummarizeEvent(event3);
            SummaryOutput data = es.Output(es.Snapshot());

            Assert.Equal(1000, data.StartDate);
            Assert.Equal(2000, data.EndDate);
        }

        [Fact]
        public void SummarizeEventIncrementsCounters()
        {
            EventSummarizer es = new EventSummarizer();
            FeatureFlag flag1 = new FeatureFlagBuilder("key1").Build();
            FeatureFlag flag2 = new FeatureFlagBuilder("key2").Build();
            string unknownFlagKey = "badkey";
            JToken default1 = new JValue("default1");
            JToken default2 = new JValue("default2");
            JToken default3 = new JValue("default3");
            Event event1 = _eventFactory.NewFeatureRequestEvent(flag1, _user,
                1, new JValue("value1"), default1);
            Event event2 = _eventFactory.NewFeatureRequestEvent(flag1, _user,
                2, new JValue("value2"), default1);
            Event event3 = _eventFactory.NewFeatureRequestEvent(flag2, _user,
                1, new JValue("value99"), default2);
            Event event4 = _eventFactory.NewFeatureRequestEvent(flag1, _user,
                1, new JValue("value1"), default1);
            Event event5 = _eventFactory.NewUnknownFeatureRequestEvent(unknownFlagKey, _user, default3);
            es.SummarizeEvent(event1);
            es.SummarizeEvent(event2);
            es.SummarizeEvent(event3);
            es.SummarizeEvent(event4);
            es.SummarizeEvent(event5);
            SummaryOutput data = es.Output(es.Snapshot());

            data.Features[flag1.Key].Counters.Sort((a, b) => ((string)a.Value).CompareTo((string)b.Value));
            EventSummaryCounter expected1 = new EventSummaryCounter(new JValue("value1"),
                flag1.Version, 2);
            EventSummaryCounter expected2 = new EventSummaryCounter(new JValue("value2"),
                flag1.Version, 1);
            EventSummaryCounter expected3 = new EventSummaryCounter(new JValue("value99"),
                flag2.Version, 1);
            EventSummaryCounter expected4 = new EventSummaryCounter(default3,
                null, 1);
            Assert.Equal(new EventSummaryFlag(default1,
                new List<EventSummaryCounter> { expected1, expected2 }),
                data.Features[flag1.Key]);
            Assert.Equal(new EventSummaryFlag(default2,
                new List<EventSummaryCounter> { expected3 }),
                data.Features[flag2.Key]);
            Assert.Equal(new EventSummaryFlag(default3,
                new List<EventSummaryCounter> { expected4 }),
                data.Features[unknownFlagKey]);
        }
    }

    internal class TestEventFactory : EventFactory
    {
        internal long Timestamp { get; set; }

        internal override long GetTimestamp()
        {
            return Timestamp;
        }
    }
}
