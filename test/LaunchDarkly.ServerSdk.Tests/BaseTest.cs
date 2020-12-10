using LaunchDarkly.Logging;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// Unit tests classes can derive from this class to take advantage of useful Xunit integrations.
    /// </summary>
    public class BaseTest
    {
        /// <summary>
        /// Using this <see cref="ILogAdapter"/> in an SDK configuration will cause logging to be sent
        /// to the Xunit output buffer for the current test method, if you used the overloaded
        /// constructor <see cref="BaseTest.BaseTest(ITestOutputHelper)"/>. If you used the empty
        /// constructor, logging is disabled. Xunit only shows the buffered output for failed tests.
        /// </summary>
        /// <seealso cref="TestLogging"/>
        protected readonly ILogAdapter testLogging;

        /// <summary>
        /// Using this <see cref="Logger"/> with an SDK component will cause logging to be sent to the
        /// Xunit output buffer for the current test method, if you used the overloaded constructor
        /// <see cref="BaseTest.BaseTest(ITestOutputHelper)"/>. If you used the empty constructor,
        /// logging is disabled. Xunit only shows the buffered output for failed tests.
        /// </summary>
        /// <seealso cref="TestLogging"/>
        protected readonly Logger testLogger;

        /// <summary>
        /// This empty constructor disables log output.
        /// </summary>
        public BaseTest()
        {
            testLogging = Logs.None;
            testLogger = TestUtils.NullLogger;
        }

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
        public BaseTest(ITestOutputHelper testOutput)
        {
            testLogging = TestLogging.TestOutputAdapter(testOutput);
            testLogger = TestLogging.TestLogger(testOutput);
        }
    }
}
