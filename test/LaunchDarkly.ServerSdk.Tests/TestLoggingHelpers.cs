using System;
using LaunchDarkly.Logging;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// Allows logging from SDK components to appear in test output.
    /// </summary>
    /// <remarks>
    /// Xunit disables all console output from unit tests, because multiple tests can run in parallel so it
    /// would be impossible to see which test produced the output. Instead, it provides an ITestOutputHelper
    /// which is passed into your test class automatically if you declare a constructor parameter for it; any
    /// output written to this object is buffered on a per-test-method basis, and dumped into the output all at
    /// once if the test method fails. We were unable to take advantage of this when we were using Common.Logging,
    /// because Common.Logging is configured globally with static methods; but now that we're using our own API,
    /// we can direct output from a component to a specific logger instance, which can be redirected to Xunit.
    /// See <see cref="BaseTest"/> for the simplest way to use this in tests.
    /// </remarks>
    public class TestLoggingHelpers
    {
        /// <summary>
        /// Creates an <see cref="ILogAdapter"/> that sends logging to the Xunit output buffer. Use this in
        /// contexts where an <c>ILogAdapter</c> is expected instead of an individual logger instance (such as
        /// in the SDK client configuration).
        /// </summary>
        /// <param name="testOutputHelper">the <see cref="ITestOutputHelper"/> that Xunit passed to the test
        /// class constructor</param>
        /// <returns>a log adapter</returns>
        public static ILogAdapter TestOutputAdapter(ITestOutputHelper testOutputHelper) =>
            Logs.ToMethod(line =>
            {
                // ITestOutputHelper.WriteLine can throw an exception if we try to write output after the
                // end of a test (for instance, from a worker task). We can ignore any such errors.
                try
                {
                    testOutputHelper.WriteLine("LOG OUTPUT >> " + line);
                }
                catch { }
            });

        /// <summary>
        /// Creates a <see cref="Logger"/> that sends logging to the Xunit output buffer. Use this when testing
        /// lower-level components that want a specific logger instance instead of an <see cref="ILogAdapter"/>.
        /// </summary>
        /// <param name="testOutputHelper">the <see cref="ITestOutputHelper"/> that Xunit passed to the test
        /// class constructor</param>
        /// <returns>a logger</returns>
        public static Logger TestLogger(ITestOutputHelper testOutputHelper) =>
            TestOutputAdapter(testOutputHelper).Logger("");

        /// <summary>
        /// Convenience property for getting a millisecond timestamp string.
        /// </summary>
        public static string TimestampString => DateTime.Now.ToString("O");
    }
}
