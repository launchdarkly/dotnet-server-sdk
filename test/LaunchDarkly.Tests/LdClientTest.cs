using System.Collections.Generic;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class LdClientTest
    {
        [Fact]
        public void EvaluatingFlagGeneratesEvent()
        {
            IFeatureStore featureStore = new InMemoryFeatureStore();
            Configuration config = Configuration.Default("secret")
                .WithOffline(true)
                .WithFeatureStore(featureStore);
            MockEventProcessor ep = new MockEventProcessor();
            LdClient client = new LdClient(config, ep);

            FeatureFlag flag = new FeatureFlagBuilder("flagkey")
                .OffVariation(0)
                .Variations(new List<JToken> { new JValue("a"), new JValue("b") })
                .Build();
            featureStore.Init(new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>());
            featureStore.Upsert(VersionedDataKind.Features, flag);

            User user = new User("user");
            client.StringVariation("flagkey", user, "default");

            Assert.Collection(ep.Events,
                e => CheckFlagEvent(e, flag, user, 0, new JValue("a"), new JValue("default")));
        }

        private void CheckFlagEvent(Event e, FeatureFlag flag, User user, int variation, JToken value, JToken defaultVal)
        {
            Assert.IsType<FeatureRequestEvent>(e);
            FeatureRequestEvent fe = e as FeatureRequestEvent;
            Assert.Equal(flag.Key, fe.Key);
            Assert.Equal(flag.Version, fe.Version);
            Assert.Equal(user, fe.User);
            Assert.Equal(variation, fe.Variation);
            Assert.Equal(value, fe.Value);
            Assert.Equal(defaultVal, fe.Default);
            Assert.Null(fe.PrereqOf);
        }

        [Fact]
        public void EvaluatingUnknownFlagGeneratesEvent()
        {
            Configuration config = Configuration.Default("secret")
                .WithOffline(true);
            MockEventProcessor ep = new MockEventProcessor();
            LdClient client = new LdClient(config, ep);

            User user = new User("user");
            client.StringVariation("badflag", user, "default");

            Assert.Collection(ep.Events,
                e => CheckUnknownFlagEvent(e, "badflag", user, new JValue("default")));
        }

        private void CheckUnknownFlagEvent(Event e, string key, User user, JToken value)
        {
            Assert.IsType<FeatureRequestEvent>(e);
            FeatureRequestEvent fe = e as FeatureRequestEvent;
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