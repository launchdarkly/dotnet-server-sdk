using System;
using System.Threading;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// Unit tests classes should derive from this class to provide necessary environment setup
    /// and useful Xunit integrations.
    /// </summary>
    public class BaseTest
    {
        protected const string BasicSdkKey = "sdk-key";
        protected static readonly User BasicUser = User.WithKey("user-key");

        /// <summary>
        /// Using this <see cref="ILogAdapter"/> in an SDK configuration will cause logging to be sent
        /// to the Xunit output buffer for the current test method, if you used the overloaded
        /// constructor <see cref="BaseTest.BaseTest(ITestOutputHelper)"/>. If you used the empty
        /// constructor, logging is disabled. Xunit only shows the buffered output for failed tests.
        /// </summary>
        /// <seealso cref="TestLogging"/>
        protected readonly ILogAdapter TestLogging;

        /// <summary>
        /// Using this <see cref="Logger"/> with an SDK component will cause logging to be sent to the
        /// Xunit output buffer for the current test method, if you used the overloaded constructor
        /// <see cref="BaseTest.BaseTest(ITestOutputHelper)"/>. If you used the empty constructor,
        /// logging is disabled. Xunit only shows the buffered output for failed tests.
        /// </summary>
        /// <seealso cref="TestLogging"/>
        protected readonly Logger TestLogger;

        /// <summary>
        /// All log output written via <see cref="TestLogger"/> or <see cref="TestLogging"/> is copied
        /// to this buffer (as well as being buffered by Xunit, if applicable).
        /// </summary>
        protected readonly LogCapture LogCapture;

        /// <summary>
        /// A minimal LdClientContext that uses the test logger.
        /// </summary>
        protected readonly LdClientContext BasicContext;

        /// <summary>
        /// A TaskExecutor instance that uses the test logger.
        /// </summary>
        protected readonly TaskExecutor BasicTaskExecutor;

        /// <summary>
        /// This empty constructor disables log output, but still captures it.
        /// </summary>
        public BaseTest() : this(Logs.None) { }

        /// <summary>
        /// This constructor allows log output to be captured by Xunit. Simply declare a constructor
        /// with the same <c>ITestOutputHelper</c> parameter for your test class and pass the parameter
        /// straight through to the base class constructor.
        /// </summary>
        /// <example>
        /// <code>
        ///     public class MyTestClass : BaseTest
        ///     {
        ///         public MyTestClass(ITestOutputHelper testOutput) : base(testOutput) { }
        ///     }
        /// </code>
        /// </example>
        /// <param name="testOutput">the <see cref="ITestOutputHelper"/> that Xunit passed to the test
        /// class constructor</param>
        public BaseTest(ITestOutputHelper testOutput) : this(TestLoggingHelpers.TestOutputAdapter(testOutput)) { }

        protected BaseTest(ILogAdapter extraLogging)
        {
            LogCapture = Logs.Capture();
            TestLogging = Logs.ToMultiple(extraLogging, LogCapture);
            TestLogger = TestLogging.Logger("");

            BasicContext = new LdClientContext(new BasicConfiguration(Configuration.Default(BasicSdkKey), TestLogger),
                Configuration.Default(""));

            BasicTaskExecutor = new TaskExecutor("test-sender", TestLogger);

            // The following line prevents intermittent test failures that happen only in .NET
            // Framework, where background tasks (including calls to Task.Delay) are very excessively
            // slow to start-- on the order of many seconds. The issue appears to be a long-standing
            // one that is described here, where the very low default setting of ThreadPool.SetMinThreads
            // causes new worker tasks to be severely throttled:
            // http://joeduffyblog.com/2006/07/08/clr-thread-pool-injection-stuttering-problems/
            //
            // We've seen experimentally that this setting defaults to 2 when running the tests in .NET
            // Framework, versus at least 12 when running them in .NET Core.
            //
            // It is theoretically possible for something similar to happen in a real application, but
            // since that would affect all tasks in the application, we assume that developers would
            // need to tune this parameter in any case, not only because of our SDK.
            ThreadPool.SetMinThreads(100, 100);
        }

        // Returns a ConfigurationBuilder with no external data source, events disabled, and logging redirected
        // to the test output. Using this as a base configuration for tests, and then overriding properties as
        // needed, protects against accidental interaction with external services and also makes it easier to
        // see which properties are important in a test.
        protected ConfigurationBuilder BasicConfig() =>
            Configuration.Builder(BasicSdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .Events(Components.NoEvents)
                .Logging(TestLogging)
                .StartWaitTime(TimeSpan.Zero);

        public void AssertLogMessageRegex(bool shouldHave, LogLevel level, string pattern)
        {
            if (LogCapture.HasMessageWithRegex(level, pattern) != shouldHave)
            {
                ThrowLogMatchException(shouldHave, level, pattern, true);
            }
        }

        public void AssertLogMessage(bool shouldHave, LogLevel level, string text)
        {
            if (LogCapture.HasMessageWithText(level, text) != shouldHave)
            {
                ThrowLogMatchException(shouldHave, level, text, true);
            }
        }

        private void ThrowLogMatchException(bool shouldHave, LogLevel level, string text, bool isRegex) =>
            throw new AssertActualExpectedException(shouldHave, !shouldHave,
                string.Format("Expected log {0} the {1} \"{2}\" at level {3}\n\nActual log output follows:\n{4}",
                    shouldHave ? "to have" : "not to have",
                    isRegex ? "pattern" : "exact message",
                    text,
                    level,
                    LogCapture.ToString()));
    }
}
