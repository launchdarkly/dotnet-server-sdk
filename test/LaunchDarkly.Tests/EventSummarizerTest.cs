using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;
using LaunchDarkly.Client;

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
            EventSummary snapshot = es.Snapshot();
            es.SummarizeEvent(_eventFactory.NewIdentifyEvent(_user));
            EventSummary snapshot2 = es.Snapshot();
            Assert.Equal(snapshot.StartDate, snapshot2.StartDate);
            Assert.Equal(snapshot.EndDate, snapshot2.EndDate);
            Assert.Equal(snapshot.Counters, snapshot2.Counters);
        }

        [Fact]
        public void SummarizeEventDoesNothingForCustomEvent()
        {
            EventSummarizer es = new EventSummarizer();
            EventSummary snapshot = es.Snapshot();
            es.SummarizeEvent(_eventFactory.NewCustomEvent("whatever", _user, null));
            EventSummary snapshot2 = es.Snapshot();
            Assert.Equal(snapshot.StartDate, snapshot2.StartDate);
            Assert.Equal(snapshot.EndDate, snapshot2.EndDate);
            Assert.Equal(snapshot.Counters, snapshot2.Counters);
        }

        [Fact]
        public void SummarizeEventSetsStartAndEndDates()
        {
            EventSummarizer es = new EventSummarizer();
            IFlagEventProperties flag = new FlagEventPropertiesBuilder("key").Build();
            _eventFactory.Timestamp = 2000;
            Event event1 = _eventFactory.NewFeatureRequestEvent(flag, _user, null, null, null);
            _eventFactory.Timestamp = 1000;
            Event event2 = _eventFactory.NewFeatureRequestEvent(flag, _user, null, null, null);
            _eventFactory.Timestamp = 1500;
            Event event3 = _eventFactory.NewFeatureRequestEvent(flag, _user, null, null, null);
            es.SummarizeEvent(event1);
            es.SummarizeEvent(event2);
            es.SummarizeEvent(event3);
            EventSummary data = es.Snapshot();

            Assert.Equal(1000, data.StartDate);
            Assert.Equal(2000, data.EndDate);
        }

        [Fact]
        public void SummarizeEventIncrementsCounters()
        {
            EventSummarizer es = new EventSummarizer();
            IFlagEventProperties flag1 = new FlagEventPropertiesBuilder("key1").Build();
            IFlagEventProperties flag2 = new FlagEventPropertiesBuilder("key2").Build();
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
            EventSummary data = es.Snapshot();

            Dictionary<EventsCounterKey, EventsCounterValue> expected = new Dictionary<EventsCounterKey, EventsCounterValue>();
            Assert.Equal(new EventsCounterValue(2, new JValue("value1"), default1),
                data.Counters[new EventsCounterKey(flag1.Key, flag1.Version, 1)]);
            Assert.Equal(new EventsCounterValue(1, new JValue("value2"), default1),
                data.Counters[new EventsCounterKey(flag1.Key, flag1.Version, 2)]);
            Assert.Equal(new EventsCounterValue(1, new JValue("value99"), default2),
                data.Counters[new EventsCounterKey(flag2.Key, flag2.Version, 1)]);
            Assert.Equal(new EventsCounterValue(1, default3, default3),
                data.Counters[new EventsCounterKey(unknownFlagKey, null, null)]);
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
