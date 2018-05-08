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
        private static readonly String HttpDateFormat = "ddd, dd MMM yyyy HH:mm:ss 'GMT'";

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
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            IdentifyEvent e = EventFactory.Default.NewIdentifyEvent(_user);
            _ep.SendEvent(e);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIdentifyEvent(item, e, _userJson));
        }
        
        [Fact]
        public void UserDetailsAreScrubbedInIdentifyEvent()
        {
            _config.WithAllAttributesPrivate(true);
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            IdentifyEvent e = EventFactory.Default.NewIdentifyEvent(_user);
            _ep.SendEvent(e);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIdentifyEvent(item, e, _scrubbedUserJson));
        }

        [Fact]
        public void IndividualFeatureEventIsQueuedWithIndexEvent()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            IFlagEventProperties flag = new FlagEventPropertiesBuilder("flagkey").Version(11).TrackEvents(true).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe, _userJson),
                item => CheckFeatureEvent(item, fe, flag, false, null),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void UserDetailsAreScrubbedInIndexEvent()
        {
            _config.WithAllAttributesPrivate(true);
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            IFlagEventProperties flag = new FlagEventPropertiesBuilder("flagkey").Version(11).TrackEvents(true).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe, _scrubbedUserJson),
                item => CheckFeatureEvent(item, fe, flag, false, null),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void FeatureEventCanContainInlineUser()
        {
            _config.WithInlineUsersInEvents(true);
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            IFlagEventProperties flag = new FlagEventPropertiesBuilder("flagkey").Version(11).TrackEvents(true).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckFeatureEvent(item, fe, flag, false, _userJson),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void UserDetailsAreScrubbedInFeatureEvent()
        {
            _config.WithAllAttributesPrivate(true);
            _config.WithInlineUsersInEvents(true);
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            IFlagEventProperties flag = new FlagEventPropertiesBuilder("flagkey").Version(11).TrackEvents(true).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckFeatureEvent(item, fe, flag, false, _scrubbedUserJson),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void IndexEventIsStillGeneratedIfInlineUsersIsTrueButFeatureEventIsNotTracked()
        {
            _config.WithInlineUsersInEvents(true);
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            IFlagEventProperties flag = new FlagEventPropertiesBuilder("flagkey").Version(11).TrackEvents(false).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe, _userJson),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void EventKindIsDebugIfFlagIsTemporarilyInDebugMode()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            long futureTime = Util.GetUnixTimestampMillis(DateTime.Now) + 1000000;
            IFlagEventProperties flag = new FlagEventPropertiesBuilder("flagkey").Version(11).DebugEventsUntilDate(futureTime).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe, _userJson),
                item => CheckFeatureEvent(item, fe, flag, true, _userJson),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void EventCanBeBothTrackedAndDebugged()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            long futureTime = Util.GetUnixTimestampMillis(DateTime.Now) + 1000000;
            IFlagEventProperties flag = new FlagEventPropertiesBuilder("flagkey").Version(11).TrackEvents(true)
                .DebugEventsUntilDate(futureTime).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe, _userJson),
                item => CheckFeatureEvent(item, fe, flag, false, null),
                item => CheckFeatureEvent(item, fe, flag, true, _userJson),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void DebugModeExpiresBasedOnClientTimeIfClientTimeIsLaterThanServerTime()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());

            // Pick a server time that is somewhat behind the client time
            long serverTime = Util.GetUnixTimestampMillis(DateTime.Now) - 20000;

            // Send and flush an event we don't care about, just to set the last server time
            _ep.SendEvent(EventFactory.Default.NewIdentifyEvent(new User("otherUser")));
            FlushAndGetEvents(AddDateHeader(OkResponse(), serverTime));

            // Now send an event with debug mode on, with a "debug until" time that is further in
            // the future than the server time, but in the past compared to the client.
            long debugUntil = serverTime + 1000;
            IFlagEventProperties flag = new FlagEventPropertiesBuilder("flagkey").Version(11).DebugEventsUntilDate(debugUntil).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            // Should get a summary event only, not a full feature event
            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe, _userJson),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void DebugModeExpiresBasedOnServerTimeIfServerTimeIsLaterThanClientTime()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());

            // Pick a server time that is somewhat ahead of the client time
            long serverTime = Util.GetUnixTimestampMillis(DateTime.Now) + 20000;

            // Send and flush an event we don't care about, just to set the last server time
            _ep.SendEvent(EventFactory.Default.NewIdentifyEvent(new User("otherUser")));
            FlushAndGetEvents(AddDateHeader(OkResponse(), serverTime));

            // Now send an event with debug mode on, with a "debug until" time that is further in
            // the future than the client time, but in the past compared to the server.
            long debugUntil = serverTime - 1000;
            IFlagEventProperties flag = new FlagEventPropertiesBuilder("flagkey").Version(11).DebugEventsUntilDate(debugUntil).Build();
            FeatureRequestEvent fe = EventFactory.Default.NewFeatureRequestEvent(flag, _user,
                1, new JValue("value"), null);
            _ep.SendEvent(fe);

            // Should get a summary event only, not a full feature event
            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe, _userJson),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void TwoFeatureEventsForSameUserGenerateOnlyOneIndexEvent()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            IFlagEventProperties flag1 = new FlagEventPropertiesBuilder("flagkey1").Version(11).TrackEvents(true).Build();
            IFlagEventProperties flag2 = new FlagEventPropertiesBuilder("flagkey2").Version(22).TrackEvents(true).Build();
            JValue value = new JValue("value");
            FeatureRequestEvent fe1 = EventFactory.Default.NewFeatureRequestEvent(flag1, _user,
                1, value, null);
            FeatureRequestEvent fe2 = EventFactory.Default.NewFeatureRequestEvent(flag2, _user,
                1, value, null);
            _ep.SendEvent(fe1);
            _ep.SendEvent(fe2);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe1, _userJson),
                item => CheckFeatureEvent(item, fe1, flag1, false, null),
                item => CheckFeatureEvent(item, fe2, flag2, false, null),
                item => CheckSummaryEvent(item));
        }

        [Fact]
        public void NonTrackedEventsAreSummarized()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            IFlagEventProperties flag1 = new FlagEventPropertiesBuilder("flagkey1").Version(11).Build();
            IFlagEventProperties flag2 = new FlagEventPropertiesBuilder("flagkey2").Version(22).Build();
            JValue value = new JValue("value");
            JValue default1 = new JValue("default1");
            JValue default2 = new JValue("default2");
            FeatureRequestEvent fe1 = EventFactory.Default.NewFeatureRequestEvent(flag1, _user,
                1, value, default1);
            FeatureRequestEvent fe2 = EventFactory.Default.NewFeatureRequestEvent(flag2, _user,
                1, value, default2);
            _ep.SendEvent(fe1);
            _ep.SendEvent(fe2);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIndexEvent(item, fe1, _userJson),
                item => CheckSummaryEventCounters(item, fe1, fe2));
        }

        [Fact]
        public void CustomEventIsQueuedWithUser()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            CustomEvent e = EventFactory.Default.NewCustomEvent("eventkey", _user, "data");
            _ep.SendEvent(e);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckIndexEvent(item, e, _userJson),
                item => CheckCustomEvent(item, e, null));
        }

        [Fact]
        public void CustomEventCanContainInlineUser()
        {
            _config.WithInlineUsersInEvents(true);
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            CustomEvent e = EventFactory.Default.NewCustomEvent("eventkey", _user, "data");
            _ep.SendEvent(e);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckCustomEvent(item, e, _userJson));
        }

        [Fact]
        public void UserDetailsAreScrubbedInCustomEvent()
        {
            _config.WithAllAttributesPrivate(true);
            _config.WithInlineUsersInEvents(true);
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            CustomEvent e = EventFactory.Default.NewCustomEvent("eventkey", _user, "data");
            _ep.SendEvent(e);

            JArray output = FlushAndGetEvents(OkResponse());
            Assert.Collection(output,
                item => CheckCustomEvent(item, e, _scrubbedUserJson));
        }

        [Fact]
        public void FinalFlushIsDoneOnDispose()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            IdentifyEvent e = EventFactory.Default.NewIdentifyEvent(_user);
            _ep.SendEvent(e);

            PrepareResponse(OkResponse());
            _ep.Dispose();

            JArray output = GetLastRequest().BodyAsJson as JArray;
            Assert.Collection(output,
                item => CheckIdentifyEvent(item, e, _userJson));
        }

        [Fact]
        public void FlushDoesNothingIfThereAreNoEvents()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            _ep.Flush();

            foreach (LogEntry le in _server.LogEntries)
            {
                Assert.True(false, "Should not have sent an HTTP request");
            }
        }

        [Fact]
        public void SdkKeyIsSent()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            Event e = EventFactory.Default.NewIdentifyEvent(_user);
            _ep.SendEvent(e);

            RequestMessage r = FlushAndGetRequest(OkResponse());

            Assert.Equal("SDK_KEY", r.Headers["Authorization"][0]);
        }

        [Fact]
        public void SchemaHeaderIsSent()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            Event e = EventFactory.Default.NewIdentifyEvent(_user);
            _ep.SendEvent(e);

            RequestMessage r = FlushAndGetRequest(OkResponse());

            Assert.Equal("3", r.Headers["X-LaunchDarkly-Event-Schema"][0]);
        }

        [Fact]
        public void NoMoreEventsArePostedAfterReceiving401Error()
        {
            _ep = new DefaultEventProcessor(_config, _config.HttpClient());
            Event e = EventFactory.Default.NewIdentifyEvent(_user);
            _ep.SendEvent(e);
            FlushAndGetEvents(Response.Create().WithStatusCode(401));
            _server.ResetLogEntries();

            _ep.SendEvent(e);
            _ep.Flush();
            foreach (LogEntry le in _server.LogEntries)
            {
                Assert.True(false, "Should not have sent an HTTP request");
            }
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

        private void CheckFeatureEvent(JToken t, FeatureRequestEvent fe, IFlagEventProperties flag, bool debug, JToken userJson)
        {
            JObject o = t as JObject;
            Assert.Equal(debug ? "debug" : "feature", (string)o["kind"]);
            Assert.Equal(fe.CreationDate, (long)o["creationDate"]);
            Assert.Equal(flag.Key, (string)o["key"]);
            Assert.Equal(flag.Version, (int)o["version"]);
            if (fe.Variation == null)
            {
                Assert.Null(o["variation"]);
            }
            else
            {
                Assert.Equal(fe.Variation, (int)o["variation"]);
            }
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
                Assert.Equal(fe.Variation, c["variation"]);
                Assert.Equal(fe.Value, c["value"]);
                Assert.Equal(fe.Version, (int)c["version"]);
                Assert.Equal(1, (int)c["count"]);
            }
        }

        private IResponseBuilder OkResponse()
        {
            return Response.Create().WithStatusCode(200);
        }

        private IResponseBuilder AddDateHeader(IResponseBuilder resp, long timestamp)
        {
            DateTime dt = Util.UnixEpoch.AddMilliseconds(timestamp);
            return resp.WithHeader("Date", dt.ToString(HttpDateFormat));
        }

        private void PrepareResponse(IResponseBuilder resp)
        {
            _server.Given(Request.Create().WithPath("/bulk").UsingPost())
                .RespondWith(resp);
            _server.ResetLogEntries();
        }

        private RequestMessage FlushAndGetRequest(IResponseBuilder resp)
        {
            PrepareResponse(resp);
            _ep.Flush();
            ((DefaultEventProcessor)_ep).WaitUntilInactive();
            return GetLastRequest();
        }

        private RequestMessage GetLastRequest()
        {
            foreach (LogEntry le in _server.LogEntries)
            {
                return le.RequestMessage;
            }
            Assert.True(false, "Did not receive a post request");
            return null;
        }

        private JArray FlushAndGetEvents(IResponseBuilder resp)
        {
            return FlushAndGetRequest(resp).BodyAsJson as JArray;
        }
    }
}
