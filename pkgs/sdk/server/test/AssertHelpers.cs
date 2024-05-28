using System;
using LaunchDarkly.Logging;
using LaunchDarkly.TestHelpers;
using Xunit;
using Xunit.Sdk;

using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;
using static LaunchDarkly.TestHelpers.JsonAssertions;

namespace LaunchDarkly.Sdk.Server
{
    public static class AssertHelpers
    {
        public static void DataSetsEqual(FullDataSet<ItemDescriptor> expected, FullDataSet<ItemDescriptor> actual) =>
            AssertJsonEqual(TestUtils.DataSetAsJson(expected), TestUtils.DataSetAsJson(actual));

        public static void DataItemsEqual(DataKind kind, ItemDescriptor expected, ItemDescriptor actual)
        {
            AssertJsonEqual(kind.Serialize(expected), kind.Serialize(actual));
            Assert.Equal(expected.Version, actual.Version);
        }

        public static void LogMessageRegex(LogCapture logCapture, bool shouldHave, LogLevel level, string pattern)
        {
            if (logCapture.HasMessageWithRegex(level, pattern) != shouldHave)
            {
                ThrowLogMatchException(logCapture, shouldHave, level, pattern, true);
            }
        }

        public static void LogMessageText(LogCapture logCapture, bool shouldHave, LogLevel level, string text)
        {
            if (logCapture.HasMessageWithText(level, text) != shouldHave)
            {
                ThrowLogMatchException(logCapture, shouldHave, level, text, true);
            }
        }

        private static void ThrowLogMatchException(LogCapture logCapture, bool shouldHave, LogLevel level, string text, bool isRegex) =>
            throw new AssertActualExpectedException(shouldHave, !shouldHave,
                string.Format("Expected log {0} the {1} \"{2}\" at level {3}\n\nActual log output follows:\n{4}",
                    shouldHave ? "to have" : "not to have",
                    isRegex ? "pattern" : "exact message",
                    text,
                    level,
                    logCapture.ToString()));

        public static void ExpectPredicate<T>(EventSink<T> sink, Predicate<T> predicate, string message, TimeSpan timeout)
        {
            while (true)
            {
                var startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var value = sink.ExpectValue();
                if (predicate(value))
                {
                    break;
                }

                if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime > timeout.TotalMilliseconds)
                {
                    // XUnit 2.5+ adds Assert.Fail.
                    Assert.True(false, message);
                }
            }
        }
    }
}
