using System.Collections.Generic;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class LdClientTest
    {
        private LdClient MakeClient(IFeatureStore featureStore, MockEventProcessor ep)
        {
            Configuration config = Configuration.Default("secret")
                .WithFeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore))
                .WithEventProcessorFactory(TestUtils.SpecificEventProcessor(ep))
                .WithUpdateProcessorFactory(Components.NullUpdateProcessor);
            LdClient client = new LdClient(config);
            featureStore.Init(new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>());
            return client;
        }

        [Fact]
        public void EvaluatingFlagGeneratesEvent()
        {
            IFeatureStore featureStore = new InMemoryFeatureStore();
            MockEventProcessor ep = new MockEventProcessor();
            LdClient client = MakeClient(featureStore, ep);

            FeatureFlag flag = new FeatureFlagBuilder("flagkey")
                .OffVariation(0)
                .Variations(new List<JToken> { new JValue("a"), new JValue("b") })
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            User user = User.WithKey("user");
            client.StringVariation("flagkey", user, "default");

            Assert.Collection(ep.Events,
                e => CheckFlagEvent(e, flag, flag.Version, user, 0, new JValue("a"), new JValue("default")));
        }

        [Fact]
        public void EvaluatingFlagWithNullUserGeneratesEvent()
        {
            IFeatureStore featureStore = new InMemoryFeatureStore();
            MockEventProcessor ep = new MockEventProcessor();
            LdClient client = MakeClient(featureStore, ep);

            FeatureFlag flag = new FeatureFlagBuilder("flagkey")
                .OffVariation(0)
                .Variations(new List<JToken> { new JValue("a"), new JValue("b") })
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);
            
            client.StringVariation("flagkey", null, "default");

            Assert.Collection(ep.Events,
                e => CheckFlagEvent(e, flag, flag.Version, null, null, new JValue("default"), new JValue("default")));
        }

        [Fact]
        public void EvaluatingUnknownFlagGeneratesEvent()
        {
            IFeatureStore featureStore = new InMemoryFeatureStore();
            MockEventProcessor ep = new MockEventProcessor();
            LdClient client = MakeClient(featureStore, ep);

            User user = User.WithKey("user");
            client.StringVariation("badflag", user, "default");

            Assert.Collection(ep.Events,
                e => CheckUnknownFlagEvent(e, "badflag", user, new JValue("default")));
        }

        [Fact]
        public void IdentifyGeneratesIdentifyEvent()
        {
            MockEventProcessor ep = new MockEventProcessor();
            LdClient client = MakeClient(new InMemoryFeatureStore(), ep);

            User user = User.WithKey("user");
            client.Identify(user);

            Assert.Collection(ep.Events,
                e => CheckIdentifyEvent(e, user));
        }
        
        [Fact]
        public void TrackGeneratesCustomEvent()
        {
            MockEventProcessor ep = new MockEventProcessor();
            LdClient client = MakeClient(new InMemoryFeatureStore(), ep);

            User user = User.WithKey("user");
            client.Track("thing", user);

            Assert.Collection(ep.Events,
                e => CheckCustomEvent(e, user, "thing", null));
        }

        [Fact]
        public void TrackWithDataGeneratesCustomEvent()
        {
            MockEventProcessor ep = new MockEventProcessor();
            LdClient client = MakeClient(new InMemoryFeatureStore(), ep);

            User user = User.WithKey("user");
            JToken data = new JValue(3);
            client.Track("thing", data, user);

            Assert.Collection(ep.Events,
                e => CheckCustomEvent(e, user, "thing", data));
        }

        [Fact]
        public void TrackWithStringDataGeneratesCustomEvent()
        {
            MockEventProcessor ep = new MockEventProcessor();
            LdClient client = MakeClient(new InMemoryFeatureStore(), ep);

            User user = User.WithKey("user");
            client.Track("thing", user, "string");

            Assert.Collection(ep.Events,
                e => CheckCustomEvent(e, user, "thing", new JValue("string")));
        }

        [Fact]
        public void SecureModeHashTest()
        {
            Configuration config = Configuration.Default("secret");
            config.WithOffline(true);
            LdClient client = new LdClient(config);

            var user = User.WithKey("Message");
            Assert.Equal("aa747c502a898200f9e4fa21bac68136f886a0e27aec70ba06daf2e2a5cb5597", client.SecureModeHash(user));
            client.Dispose();
        }

        private void CheckFlagEvent(Event e, FeatureFlag flag, int? version, User user, int? variation, JToken value, JToken defaultVal)
        {
            FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
            Assert.Equal(flag.Key, fe.Key);
            Assert.Equal(version, fe.Version);
            Assert.Equal(user, fe.User);
            Assert.Equal(variation, fe.Variation);
            Assert.Equal(value, fe.Value);
            Assert.Equal(defaultVal, fe.Default);
            Assert.Null(fe.PrereqOf);
        }

        private void CheckUnknownFlagEvent(Event e, string key, User user, JToken value)
        {
            FeatureRequestEvent fe = Assert.IsType<FeatureRequestEvent>(e);
            Assert.Equal(key, fe.Key);
            Assert.Null(fe.Version);
            Assert.Equal(user, fe.User);
            Assert.Null(fe.Variation);
            Assert.Equal(value, fe.Value);
            Assert.Equal(value, fe.Default);
            Assert.Null(fe.PrereqOf);
        }

        private void CheckIdentifyEvent(Event e, User user)
        {
            IdentifyEvent ie = Assert.IsType<IdentifyEvent>(e);
            Assert.Equal(user.Key, ie.Key);
            Assert.Equal(user, ie.User);
        }

        private void CheckCustomEvent(Event e, User user, string key, JToken data)
        {
            CustomEvent ce = Assert.IsType<CustomEvent>(e);
            Assert.Equal(key, ce.Key);
            Assert.Equal(user, ce.User);
            Assert.Equal(data, ce.JsonData);
        }
    }

    internal class MockEventProcessor : IEventProcessor
    {
        internal List<Event> Events = new List<Event>();

        public void SendEvent(Event e)
        {
            Events.Add(e);
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }
    }
}