using LaunchDarkly.Logging;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class LoggingConfigurationBuilderTest
    {
        [Fact]
        public void HasNonNullDefaultLogAdapter()
        {
            var logConfig = Components.Logging().CreateLoggingConfiguration();
            Assert.NotNull(logConfig.LogAdapter);
        }

        [Fact]
        public void CanSpecifyAdapter()
        {
            var adapter = Logs.ToMultiple(Logs.None);

            var logConfig1 = Components.Logging()
                .Adapter(adapter)
                .CreateLoggingConfiguration();
            Assert.Same(adapter, logConfig1.LogAdapter);

            var logConfig2 = Components.Logging(adapter)
                .CreateLoggingConfiguration();
            Assert.Same(adapter, logConfig2.LogAdapter);
        }

        [Fact]
        public void CanSpecifyBaseLoggerName()
        {
            var logConfig1 = Components.Logging().CreateLoggingConfiguration();
            Assert.Null(logConfig1.BaseLoggerName);

            var logConfig2 = Components.Logging().BaseLoggerName("xyz").CreateLoggingConfiguration();
            Assert.Equal("xyz", logConfig2.BaseLoggerName);
        }

        [Fact]
        public void DoesNotSetDefaultLevelForCustomAdapter()
        {
            var logCapture = Logs.Capture();
            var logConfig = Components.Logging(logCapture)
                .CreateLoggingConfiguration();
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
                .CreateLoggingConfiguration();
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
            var logConfig = Components.NoLogging.CreateLoggingConfiguration();
            Assert.Same(Logs.None, logConfig.LogAdapter);
        }
    }
}
