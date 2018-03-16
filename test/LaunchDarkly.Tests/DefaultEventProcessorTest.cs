using System;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using WireMock;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class DefaultEventProcessorTest : IDisposable
    {
        private Configuration _config = Configuration.Default("SDK_KEY");
        private IEventProcessor _ep;
        private FluentMockServer _server;
        private User _user = new User("userKey").AndName("Red");
        private JToken _userJson = JToken.Parse("{\"key\":\"userKey\",\"name\":\"Red\"}");
        private JToken _scrubbedUserJson = JToken.Parse("{\"key\":\"userKey\",\"privateAttrs\":[\"name\"]}");

        public DefaultEventProcessorTest()
        {
            _server = FluentMockServer.Start();
            _config.WithEventsUri(_server.Urls[0]);
        }

        void IDisposable.Dispose()
        {
            _server.Stop();
            if (_ep != null)
            {
                _ep.Dispose();
            }
        }

        [Fact]
        public void IdentifyEventIsQueued()
        {
            _ep = new DefaultEventProcessor(_config);
            IdentifyEvent e = EventFactory.Default.NewIdentifyEvent(_user);
            _ep.SendEvent(e);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckIdentifyEvent(item, e, _userJson));
        }
        
        [Fact]
        public void UserDetailsAreScrubbedInIdentifyEvent()
        {
            _config.WithAllAttributesPrivate(true);
            _ep = new DefaultEventProcessor(_config);
            IdentifyEvent e = EventFactory.Default.NewIdentifyEvent(_user);
            _ep.SendEvent(e);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckIdentifyEvent(item, e, _scrubbedUserJson));
        }

        [Fact]
        public void IndividualFeatureEventIsQueuedWithIndexEvent()
        {
            _ep = new DefaultEventProcessor(_config);
            FeatureFlag flag = new FeatureFlagBuilder("flagkey").Version(11).TrackEvents(true).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe, _userJson),
                item => CheckFeatureEvent(item, fe, flag, false, null),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void UserDetailsAreScrubbedInIndexEvent()
        {
            _config.WithAllAttributesPrivate(true);
            _ep = new DefaultEventProcessor(_config);
            FeatureFlag flag = new FeatureFlagBuilder("flagkey").Version(11).TrackEvents(true).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe, _scrubbedUserJson),
                item => CheckFeatureEvent(item, fe, flag, false, null),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void FeatureEventCanContainInlineUser()
        {
            _config.WithInlineUsersInEvents(true);
            _ep = new DefaultEventProcessor(_config);
            FeatureFlag flag = new FeatureFlagBuilder("flagkey").Version(11).TrackEvents(true).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckFeatureEvent(item, fe, flag, false, _userJson),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void UserDetailsAreScrubbedInFeatureEvent()
        {
            _config.WithAllAttributesPrivate(true);
            _config.WithInlineUsersInEvents(true);
            _ep = new DefaultEventProcessor(_config);
            FeatureFlag flag = new FeatureFlagBuilder("flagkey").Version(11).TrackEvents(true).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckFeatureEvent(item, fe, flag, false, _scrubbedUserJson),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void EventKindIsDebugIfFlagIsTemporarilyInDebugMode()
        {
            _ep = new DefaultEventProcessor(_config);
            long futureTime = Util.GetUnixTimestampMillis(DateTime.Now) + 1000000;
            FeatureFlag flag = new FeatureFlagBuilder("flagkey").Version(11).DebugEventsUntilDate(futureTime).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe, _userJson),
                item => CheckFeatureEvent(item, fe, flag, true, null),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void TwoFeatureEventsForSameUserGenerateOnlyOneIndexEvent()
        {
            _ep = new DefaultEventProcessor(_config);
            FeatureFlag flag1 = new FeatureFlagBuilder("flagkey1").Version(11).TrackEvents(true).Build();
            FeatureFlag flag2 = new FeatureFlagBuilder("flagkey2").Version(22).TrackEvents(true).Build();
            JValue value = new JValue("value");
            FeatureRequestEvent fe1 = EventFactory.Default.NewFeatureRequestEvent(flag1, _user,
                1, value, null);
            FeatureRequestEvent fe2 = EventFactory.Default.NewFeatureRequestEvent(flag2, _user,
                1, value, null);
            _ep.SendEvent(fe1);
            _ep.SendEvent(fe2);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe1, _userJson),
                item => CheckFeatureEvent(item, fe1, flag1, false, null),
                item => CheckFeatureEvent(item, fe2, flag2, false, null),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void NonTrackedEventsAreSummarized()
        {
            _ep = new DefaultEventProcessor(_config);
            FeatureFlag flag1 = new FeatureFlagBuilder("flagkey1").Version(11).Build();
            FeatureFlag flag2 = new FeatureFlagBuilder("flagkey2").Version(22).Build();
            JValue value = new JValue("value");
            JValue default1 = new JValue("default1");
            JValue default2 = new JValue("default2");
            FeatureRequestEvent fe1 = EventFactory.Default.NewFeatureRequestEvent(flag1, _user,
                1, value, default1);
            FeatureRequestEvent fe2 = EventFactory.Default.NewFeatureRequestEvent(flag2, _user,
                1, value, default2);
            _ep.SendEvent(fe1);
            _ep.SendEvent(fe2);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe1, _userJson),
                item => CheckSummaryEventCounters(item, fe1, fe2));
        }

        [Fact]
        public void CustomEventIsQueuedWithUser()
        {
            _ep = new DefaultEventProcessor(_config);
            CustomEvent e = EventFactory.Default.NewCustomEvent("eventkey", _user, "data");
            _ep.SendEvent(e);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckIndexEvent(item, e, _userJson),
                item => CheckCustomEvent(item, e, null));
        }

        [Fact]
        public void CustomEventCanContainInlineUser()
        {
            _config.WithInlineUsersInEvents(true);
            _ep = new DefaultEventProcessor(_config);
            CustomEvent e = EventFactory.Default.NewCustomEvent("eventkey", _user, "data");
            _ep.SendEvent(e);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckCustomEvent(item, e, _userJson));
        }

        [Fact]
        public void UserDetailsAreScrubbedInCustomEvent()
        {
            _config.WithAllAttributesPrivate(true);
            _config.WithInlineUsersInEvents(true);
            _ep = new DefaultEventProcessor(_config);
            CustomEvent e = EventFactory.Default.NewCustomEvent("eventkey", _user, "data");
            _ep.SendEvent(e);

            JArray output = FlushAndGetEvents();
            Assert.Collection(output,
                item => CheckCustomEvent(item, e, _scrubbedUserJson));
        }

        [Fact]
        public void SdkKeyIsSent()
        {
            _ep = new DefaultEventProcessor(_config);
            Event e = EventFactory.Default.NewIdentifyEvent(_user);
            _ep.SendEvent(e);

            RequestMessage r = FlushAndGetRequest();

            Assert.Equal("SDK_KEY", r.Headers["Authorization"][0]);
        }

        private JObject MakeUserJson(User user)
        {
            return JObject.FromObject(EventUser.FromUser(user, _config));
        }

        private void CheckIdentifyEvent(JToken t, IdentifyEvent ie, JToken userJson)
        {
            JObject o = t as JObject;
            Assert.Equal("identify", (string)o["kind"]);
            Assert.Equal(ie.CreationDate, (long)o["creationDate"]);
            Assert.Equal(userJson, o["user"]);
        }

        private void CheckIndexEvent(JToken t, Event sourceEvent, JToken userJson)
        {
            JObject o = t as JObject;
            Assert.Equal("index", (string)o["kind"]);
            Assert.Equal(sourceEvent.CreationDate, (long)o["creationDate"]);
            Assert.Equal(userJson, o["user"]);
        }

        private void CheckFeatureEvent(JToken t, FeatureRequestEvent fe, FeatureFlag flag, bool debug, JToken userJson)
        {
            JObject o = t as JObject;
            Assert.Equal(debug ? "debug" : "feature", (string)o["kind"]);
            Assert.Equal(fe.CreationDate, (long)o["creationDate"]);
            Assert.Equal(flag.Key, (string)o["key"]);
            Assert.Equal(flag.Version, (int)o["version"]);
            Assert.Equal(fe.Value, o["value"]);
            CheckEventUserOrKey(o, fe, userJson);
        }

        private void CheckCustomEvent(JToken t, CustomEvent e, JToken userJson)
        {
            JObject o = t as JObject;
            Assert.Equal("custom", (string)o["kind"]);
            Assert.Equal(e.Key, (string)o["key"]);
            Assert.Equal(e.Data, (string)o["data"]);
            CheckEventUserOrKey(o, e, userJson);
        }

        private void CheckEventUserOrKey(JObject o, Event e, JToken userJson)
        {
            if (userJson != null)
            {
                Assert.Equal(userJson, o["user"]);
                Assert.Null(o["userKey"]);
            }
            else
            {
                Assert.Null(o["user"]);
                Assert.Equal(e.User.Key, (string)o["userKey"]);
            }
        }
        private void CheckSummaryEvent(JToken t)
        {
            JObject o = t as JObject;
            Assert.Equal("summary", (string)o["kind"]);
        }

        private void CheckSummaryEventCounters(JToken t, params FeatureRequestEvent[] fes)
        {
            CheckSummaryEvent(t);
            JObject o = t as JObject;
            Assert.Equal(fes[0].CreationDate, (long)o["startDate"]);
            Assert.Equal(fes[fes.Length - 1].CreationDate, (long)o["endDate"]);
            foreach (FeatureRequestEvent fe in fes)
            {
                JObject fo = (o["features"] as JObject)[fe.Key] as JObject;
                Assert.NotNull(fo);
                Assert.Equal(fe.Default, fo["default"]);
                JArray cs = fo["counters"] as JArray;
                Assert.NotNull(cs);
                Assert.Equal(1, cs.Count);
                JObject c = cs[0] as JObject;
                Assert.Equal(fe.Value, c["value"]);
                Assert.Equal(fe.Version, (int)c["version"]);
                Assert.Equal(1, (int)c["count"]);
            }
        }

        private RequestMessage FlushAndGetRequest()
        {
            _server.Given(Request.Create().WithPath("/bulk").UsingPost())
                .RespondWith(Response.Create().WithStatusCode(200));
            _ep.Flush();
            foreach (LogEntry le in _server.LogEntries)
            {
                return le.RequestMessage;
            }
            Assert.True(false, "Did not receive a post request");
            return null;
        }

        private JArray FlushAndGetEvents()
        {
            return FlushAndGetRequest().BodyAsJson as JArray;
        }
    }
}
