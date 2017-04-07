using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace LaunchDarkly.Tests {

    public class OperatorTest
    {

        private readonly Configuration _configuration = Configuration.Default("sdk-test");

        [Fact]
        public void CanCompareTimestampsFromDifferentTimezones()
        {
            var afterTimestamp = "1970-01-01T00:00:00-01:00"; //equivalent to 1970-01-01T01:00:00Z
            var utcTimestamp = "1970-01-01T00:00:01Z";

            DateTime afterDateTime = DateTime.Parse(afterTimestamp);
            JValue before = new JValue(utcTimestamp);
            DateTime beforeDateTime = Util.ObjectToDateTime(utcTimestamp);

            Assert.True(Operator.Apply("after", afterDateTime, before, _configuration));
            Assert.False(Operator.Apply("after", beforeDateTime, new JValue(afterTimestamp), _configuration));

            Assert.True(Operator.Apply("before", beforeDateTime, new JValue(afterTimestamp), _configuration));
            Assert.False(Operator.Apply("before", afterDateTime, before, _configuration));
        }

        [Fact]
        public void CanCompareTimestampWithUnixMillis()
        {
            var afterTimestamp = "1970-01-01T00:00:00-01:00"; //equivalent to 1970-01-01T01:00:00Z
            var beforeMillis = 1000;

            DateTime afterDateTime = DateTime.Parse(afterTimestamp);
            DateTime beforeDateTime = Util.ObjectToDateTime(beforeMillis);
            JValue before = new JValue(beforeMillis);

            Assert.True(Operator.Apply("after", afterDateTime, before, _configuration));
            Assert.False(Operator.Apply("after", beforeDateTime, new JValue(afterTimestamp), _configuration));

            Assert.True(Operator.Apply("before", beforeDateTime, new JValue(afterTimestamp), _configuration));
            Assert.False(Operator.Apply("before", afterDateTime, before, _configuration));
        }

        [Fact]
        public void Apply_UnknownOperation_ReturnsFalse()
        {
            Assert.False(Operator.Apply("unknown", 10, new JValue(10), _configuration));
        }

        [Fact]
        public void Apply_UserValueIsNull_ReturnsFalse()
        {
            Assert.False(Operator.Apply("in", null, new JValue(10), _configuration));
        }

        [Fact]
        public void Apply_ClauseValueIsNull_ReturnsFalse()
        {
            Assert.False(Operator.Apply("in", 10, JValue.CreateNull(), _configuration));
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData("hello", "hello")]
        [InlineData(12.5d, 12.5d)]
        [InlineData(11, 11d)]
        [InlineData(11d, 11)]
        public void Apply_In_SupportedTypes_ValuesAreEqual_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("in", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData(11, 60)]
        [InlineData("first", "second")]
        [InlineData(745.4d, 92.5d)]
        [InlineData(67, 92.5d)]
        [InlineData(11.4d, 11)]
        public void Apply_In_SupportedTypes_ValuesAreNotEqual_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("in", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData("userValue", "Value")]
        [InlineData("userValue", "userValue")]
        public void Apply_EndsWith_SupportedTypes_ReturnsTrue(string userValue, string clauseValue)
        {
            Assert.True(Operator.Apply("endsWith", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_EndsWith_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("endsWith", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData("userValue", "userV")]
        [InlineData("userValue", "user")]
        public void Apply_StartsWith_SupportedTypes_ReturnsTrue(string userValue, string clauseValue)
        {
            Assert.True(Operator.Apply("startsWith", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_StartsWith_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("startsWith", userValue, new JValue(clauseValue), _configuration));
        }

        [Fact]
        public void Apply_Matches_SupportedTypes_ReturnsTrue()
        {
            Assert.True(Operator.Apply("matches", "22", new JValue(@"\d"), _configuration));
        }

        [Theory]
        [InlineData("Some text", @"\d")]
        [InlineData(@"\d", "22")]
        [InlineData("userValue", 77)]
        [InlineData(77, "userValue")]
        public void Apply_Matches_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("matches", userValue, new JValue(clauseValue), _configuration));
        }

        [Fact]
        public void Apply_Contains_SupportedTypes_ReturnsTrue()
        {
            Assert.True(Operator.Apply("contains", "userValue", new JValue("serValu"), _configuration));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_Contains_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("contains", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData(10, 11)]
        [InlineData(55d, 66)]
        [InlineData(55, 66d)]
        public void Apply_LessThan_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("lessThan", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(10, 9)]
        [InlineData(55d, 55)]
        [InlineData(55d, 54)]
        [InlineData(55, 55d)]
        [InlineData(55, 54d)]
        [InlineData(55d, 55d)]
        [InlineData(55d, 54d)]
        public void Apply_LessThan_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("lessThan", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData(99, 99)]
        [InlineData(77, 78)]
        [InlineData(46d, 46)]
        [InlineData(33d, 34d)]
        [InlineData(33d, 33d)]
        [InlineData(32, 34d)]
        public void Apply_LessThanOrEqual_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("lessThanOrEqual", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData(999, 99)]
        [InlineData(79, 78)]
        [InlineData(46d, 45)]
        [InlineData(33d, 32d)]
        [InlineData(23, 22d)]
        public void Apply_LessThanOrEqual_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("lessThanOrEqual", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData(11, 10)]
        [InlineData(66, 55d)]
        [InlineData(67d, 56)]
        public void Apply_GreaterThan_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("greaterThan", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(9, 10)]
        [InlineData(55, 55d)]
        [InlineData(54, 55d)]
        [InlineData(55d, 55)]
        [InlineData(54d, 55)]
        [InlineData(55d, 55d)]
        [InlineData(54d, 55d)]
        public void Apply_GreaterThan_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("greaterThan", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData(99, 99)]
        [InlineData(78, 77)]
        [InlineData(46, 46d)]
        [InlineData(34d, 33d)]
        [InlineData(33d, 33d)]
        [InlineData(34d, 32)]
        public void Apply_GreaterThanOrEqual_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("greaterThanOrEqual", userValue, new JValue(clauseValue), _configuration));
        }

        [Theory]
        [InlineData(99, 999)]
        [InlineData(78, 79)]
        [InlineData(45, 46d)]
        [InlineData(32d, 33d)]
        [InlineData(22d, 23)]
        public void Apply_GreaterThanOrEqual_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("greaterThanOrEqual", userValue, new JValue(clauseValue), _configuration));
        }

    }

}