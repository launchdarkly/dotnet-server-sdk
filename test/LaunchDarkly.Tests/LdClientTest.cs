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
                .WithOffline(true)
                .WithFeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore))
                .WithEventProcessorFactory(TestUtils.SpecificEventProcessor(ep));
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

            User user = new User("user");
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

        [Fact]
        public void EvaluatingUnknownFlagGeneratesEvent()
        {
            IFeatureStore featureStore = new InMemoryFeatureStore();
            MockEventProcessor ep = new MockEventProcessor();
            LdClient client = MakeClient(featureStore, ep);

            User user = new User("user");
            client.StringVariation("badflag", user, "default");

            Assert.Collection(ep.Events,
                e => CheckUnknownFlagEvent(e, "badflag", user, new JValue("default")));
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