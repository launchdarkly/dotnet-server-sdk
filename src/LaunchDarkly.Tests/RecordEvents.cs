using System;
using System.Threading;
using Moq;
using NUnit.Framework;
using LaunchDarkly.Client;
using LaunchDarkly.Client.Logging;
using NLog;
using NLog.Targets;
using System.Net.Http;

namespace LaunchDarkly.Tests
{
    public class RecordEvents
    {
        [Test]
        public async void EventQueueGetsCleared()
        {
            var target = CreateInMemoryTarget();

            var config = Configuration.Default()
                                      .WithEventQueueCapacity(5)
                                      .WithEventQueueFrequency(TimeSpan.FromSeconds(1));

            var client = new LdClient(config);

            var user = User.WithKey("user@test.com");

            await client.Toggle("new.dashboard.enable1", user);
            await client.Toggle("new.dashboard.enable2", user);
            await client.Toggle("new.dashboard.enable3", user);
            await client.Toggle("new.dashboard.enable4", user);
            await client.Toggle("new.dashboard.enable5", user);

            Assert.AreEqual(5, target.Logs.Count);

            Thread.Sleep(1500);
            await client.Toggle("new.dashboard.enable6", user);
            await client.Toggle("new.dashboard.enable7", user);
            await client.Toggle("new.dashboard.enable8", user);
            await client.Toggle("new.dashboard.enable9", user);
            await client.Toggle("new.dashboard.enable10", user);

        }


        [Test]
        public async void CheckingAFeatureFlag_RaisesAFeatureEvent()
        {
            var config = Configuration.Default();
            var eventStore = new Mock<IStoreEvents>();
            var mockHttp = new Mock<HttpClient>();
            var client = new LdClient(config, mockHttp.Object, eventStore.Object);
            var user = User.WithKey("user@test.com");

            await client.Toggle("new.dashboard.enable", user);

            eventStore.Verify(s=>s.Add(It.IsAny<FeatureRequestEvent<Boolean>>()));
        }

        [Test]
        public void CanRaiseACustomEvent()
        {
            var config = Configuration.Default();
            var eventStore = new Mock<IStoreEvents>();
            var mockHttp = new Mock<HttpClient>();
            var client = new LdClient(config, mockHttp.Object, eventStore.Object);
            var user = User.WithKey("user@test.com");

            client.Track("AnyEventName", user, "AnyJson");

            eventStore.Verify(s => s.Add(It.IsAny<CustomEvent>()));
        }


        private MemoryTarget CreateInMemoryTarget()
        {
            var target = new MemoryTarget { Layout = "${message}" };
            NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(target, NLog.LogLevel.Debug);

            return target;
        }
    }
}
