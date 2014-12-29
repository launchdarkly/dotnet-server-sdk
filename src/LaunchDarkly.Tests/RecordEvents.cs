using System;
using System.Threading;
using Moq;
using NUnit.Framework;
using LaunchDarkly.Client;
using LaunchDarkly.Client.Logging;
using NLog;
using NLog.Targets;

namespace LaunchDarkly.Tests
{
    public class RecordEvents
    {




        [Test]
        public void EventQueueGetsCleared()
        {
            var target = CreateInMemoryTarget();

            var config = Configuration.Default()
                                      .WithEventQueueCapacity(5)
                                      .WithEventQueueFrequency(TimeSpan.FromSeconds(1));

            var client = new LdClient(config);

            var user = User.WithKey("user@test.com");

            client.GetFlag("new.dashboard.enable1", user);
            client.GetFlag("new.dashboard.enable2", user);
            client.GetFlag("new.dashboard.enable3", user);
            client.GetFlag("new.dashboard.enable4", user);
            client.GetFlag("new.dashboard.enable5", user);

            Assert.AreEqual(5, target.Logs.Count);

            Thread.Sleep(1500);
            client.GetFlag("new.dashboard.enable6", user);
            client.GetFlag("new.dashboard.enable7", user);
            client.GetFlag("new.dashboard.enable8", user);
            client.GetFlag("new.dashboard.enable9", user);
            client.GetFlag("new.dashboard.enable10", user);

        }


        [Test]
        public void CheckingAFeatureFlag_RaisesAFeatureEvent()
        {
            var config = Configuration.Default();
            var eventStore = new Mock<IStoreEvents>();
            var client = new LdClient(config, eventStore.Object);
            var user = User.WithKey("user@test.com");

            client.GetFlag("new.dashboard.enable", user);

            eventStore.Verify(s=>s.Add(It.IsAny<FeatureRequestEvent<Boolean>>()));
        }

        [Test]
        public void CanRaiseACustomEvent()
        {
            var config = Configuration.Default();
            var eventStore = new Mock<IStoreEvents>();
            var client = new LdClient(config, eventStore.Object);
            var user = User.WithKey("user@test.com");

            client.SendEvent("AnyEventName", user, "AnyJson");

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
