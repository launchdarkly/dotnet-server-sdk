
using NUnit.Framework;
using LaunchDarkly.Client.Logging;
using NLog;
using NLog.Targets;
using LaunchDarkly.Client;


namespace LaunchDarkly.Tests
{
    public class Logging
    {
        [Test]
        public void ExceedingQueueCapacity_LogsAWarning()
        {
            var target = CreateInMemoryTarget(NLog.LogLevel.Warn);

            var config = Configuration.Default()
                                      .WithEventQueueCapacity(2);

            var client = new LdClient(config);
            var user = User.WithKey("user@test.com");

            client.GetFlag("new.dashboard.enable", user);
            client.GetFlag("image.hover", user);
            client.GetFlag("sort.order", user);

            Assert.AreEqual(1, target.Logs.Count);
        }


        private MemoryTarget CreateInMemoryTarget(NLog.LogLevel logLevel)
        {
            var target = new MemoryTarget { Layout = "${message}" };
            NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(target, logLevel);

            return target;
        }

    }
}
