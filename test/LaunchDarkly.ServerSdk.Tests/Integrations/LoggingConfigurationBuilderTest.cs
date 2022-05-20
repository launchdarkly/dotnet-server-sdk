using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class LoggingConfigurationBuilderTest
    {
        private static readonly LdClientContext basicContext = new LdClientContext("");

        [Fact]
        public void HasNonNullDefaultLogAdapter()
        {
            var logConfig = Components.Logging().Build(basicContext);
            Assert.NotNull(logConfig.LogAdapter);
        }

        [Fact]
        public void CanSpecifyAdapter()
        {
            var adapter = Logs.ToMultiple(Logs.None);

            var logConfig1 = Components.Logging()
                .Adapter(adapter)
                .Build(basicContext);
            Assert.Same(adapter, logConfig1.LogAdapter);

            var logConfig2 = Components.Logging(adapter)
                .Build(basicContext);
            Assert.Same(adapter, logConfig2.LogAdapter);
        }

        [Fact]
        public void CanSpecifyBaseLoggerName()
        {
            var logConfig1 = Components.Logging().Build(basicContext);
            Assert.Null(logConfig1.BaseLoggerName);

            var logConfig2 = Components.Logging().BaseLoggerName("xyz").Build(basicContext);
            Assert.Equal("xyz", logConfig2.BaseLoggerName);
        }

        [Fact]
        public void DoesNotSetDefaultLevelForCustomAdapter()
        {
            var logCapture = Logs.Capture();
            var logConfig = Components.Logging(logCapture)
                .Build(basicContext);
            var logger = logConfig.LogAdapter.Logger("");
            logger.Debug("hi");
            Assert.True(logCapture.HasMessageWithText(LogLevel.Debug, "hi"));
        }

        [Fact]
        public void CanOverrideLevel()
        {
            var logCapture = Logs.Capture();
            var logConfig = Components.Logging(logCapture)
                .Level(LogLevel.Warn)
                .Build(basicContext);
            var logger = logConfig.LogAdapter.Logger("");
            logger.Debug("d");
            logger.Info("i");
            logger.Warn("w");
            Assert.False(logCapture.HasMessageWithText(LogLevel.Debug, "d"));
            Assert.False(logCapture.HasMessageWithText(LogLevel.Info, "i"));
            Assert.True(logCapture.HasMessageWithText(LogLevel.Warn, "w"));
        }

        [Fact]
        public void NoLoggingIsShortcutForLogsNone()
        {
            var logConfig = Components.NoLogging.Build(basicContext);
            Assert.Same(Logs.None, logConfig.LogAdapter);
        }
    }
}
