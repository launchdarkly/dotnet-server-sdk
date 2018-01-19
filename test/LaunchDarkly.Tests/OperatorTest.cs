using System;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{

    public class OperatorTest
    {

        [Fact]
        public void CanParseUtcTimestamp()
        {
            var timestamp = "1970-01-01T00:00:01Z";
            var jValueToDateTime = Operator.JValueToDateTime(new JValue(timestamp));

            var expectedDateTime = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc);
            Assert.Equal(expectedDateTime, jValueToDateTime);
        }

        [Fact]
        public void CanParseTimestampFromTimezone()
        {
            var timestamp = "1970-01-01T00:00:00-01:00";
            var jValueToDateTime = Operator.JValueToDateTime(new JValue(timestamp));
            var expectedDateTime = new DateTime(1970, 1, 1, 1, 0, 0, DateTimeKind.Utc);
            Assert.Equal(expectedDateTime, jValueToDateTime);
        }

        [Fact]
        public void CanParseUnixMillis()
        {
            var timestampMillis = 1000;
            var jValueToDateTime = Operator.JValueToDateTime(new JValue(timestampMillis));
            var expectedDateTime = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc);
            Assert.Equal(expectedDateTime, jValueToDateTime);
        }

        [Fact]
        public void CanCompareTimestampsFromDifferentTimezones()
        {
            var afterTimestamp = "1970-01-01T00:00:00-01:00"; //equivalent to 1970-01-01T01:00:00Z
            var utcTimestamp = "1970-01-01T00:00:01Z";

            var after = new JValue(afterTimestamp);
            var before = new JValue(utcTimestamp);
            Assert.True(Operator.Apply("after", after, before));
            Assert.False(Operator.Apply("after", before, after));

            Assert.True(Operator.Apply("before", before, after));
            Assert.False(Operator.Apply("before", after, before));
        }

        [Fact]
        public void CanCompareTimestampWithUnixMillis()
        {
            var afterTimestamp = "1970-01-01T00:00:00-01:00"; //equivalent to 1970-01-01T01:00:00Z
            var beforeMillis = 1000;

            var after = new JValue(afterTimestamp);
            var before = new JValue(beforeMillis);

            Assert.True(Operator.Apply("after", after, before));
            Assert.False(Operator.Apply("after", before, after));

            Assert.True(Operator.Apply("before", before, after));
            Assert.False(Operator.Apply("before", after, before));
        }

        [Fact]
        public void Apply_UnknownOperation_ReturnsFalse()
        {
            Assert.False(Operator.Apply("unknown", new JValue(10), new JValue(10)));
        }

        [Fact]
        public void Apply_UserValueIsNull_ReturnsFalse()
        {
            Assert.False(Operator.Apply("in", null, new JValue(10)));
        }

        [Fact]
        public void Apply_ClauseValueIsNull_ReturnsFalse()
        {
            Assert.False(Operator.Apply("in", new JValue(10), null));
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData("hello", "hello")]
        [InlineData(12.5d, 12.5d)]
        [InlineData(11, 11d)]
        [InlineData(11d, 11)]
        public void Apply_In_SupportedTypes_ValuesAreEqual_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("in", new JValue(userValue), new JValue(clauseValue)));
        }

        [Theory]
        [InlineData(11, 60)]
        [InlineData("first", "second")]
        [InlineData(745.4d, 92.5d)]
        [InlineData(67, 92.5d)]
        [InlineData(11.4d, 11)]
        public void Apply_In_SupportedTypes_ValuesAreNotEqual_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("in", new JValue(userValue), new JValue(clauseValue)));
        }

        [Theory]
        [InlineData("userValue", "Value")]
        [InlineData("userValue", "userValue")]
        public void Apply_EndsWith_SupportedTypes_ReturnsTrue(string userValue, string clauseValue)
        {
            Assert.True(Operator.Apply("endsWith", new JValue(userValue), new JValue(clauseValue)));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_EndsWith_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("endsWith", new JValue(userValue), new JValue(clauseValue)));
        }

        [Theory]
        [InlineData("userValue", "userV")]
        [InlineData("userValue", "user")]
        public void Apply_StartsWith_SupportedTypes_ReturnsTrue(string userValue, string clauseValue)
        {
            Assert.True(Operator.Apply("startsWith", new JValue(userValue), new JValue(clauseValue)));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_StartsWith_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("startsWith", new JValue(userValue), new JValue(clauseValue)));
        }

        [Fact]
        public void Apply_Matches_SupportedTypes_ReturnsTrue()
        {
            Assert.True(Operator.Apply("matches", new JValue("22"), new JValue(@"\d")));
        }

        [Theory]
        [InlineData("Some text", @"\d")]
        [InlineData(@"\d", "22")]
        [InlineData("userValue", 77)]
        [InlineData(77, "userValue")]
        public void Apply_Matches_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("matches", new JValue(userValue), new JValue(clauseValue)));
        }

        [Fact]
        public void Apply_Contains_SupportedTypes_ReturnsTrue()
        {
            Assert.True(Operator.Apply("contains", new JValue("userValue"), new JValue("serValu")));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_Contains_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("contains", new JValue(userValue), new JValue(clauseValue)));
        }

        [Theory]
        [InlineData(10, 11)]
        [InlineData(55d, 66)]
        public void Apply_LessThan_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("lessThan", new JValue(userValue), new JValue(clauseValue)));
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
            Assert.False(Operator.Apply("lessThan", new JValue(userValue), new JValue(clauseValue)));
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
            Assert.True(Operator.Apply("lessThanOrEqual", new JValue(userValue), new JValue(clauseValue)));
        }

        [Theory]
        [InlineData(999, 99)]
        [InlineData(79, 78)]
        [InlineData(46d, 45)]
        [InlineData(33d, 32d)]
        [InlineData(23, 22d)]
        public void Apply_LessThanOrEqual_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("lessThanOrEqual", new JValue(userValue), new JValue(clauseValue)));
        }

        [Theory]
        [InlineData(11, 10)]
        [InlineData(66, 55d)]
        public void Apply_GreaterThan_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("greaterThan", new JValue(userValue), new JValue(clauseValue)));
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
            Assert.False(Operator.Apply("greaterThan", new JValue(userValue), new JValue(clauseValue)));
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
            Assert.True(Operator.Apply("greaterThanOrEqual", new JValue(userValue), new JValue(clauseValue)));
        }

        [Theory]
        [InlineData(99, 999)]
        [InlineData(78, 79)]
        [InlineData(45, 46d)]
        [InlineData(32d, 33d)]
        [InlineData(22d, 23)]
        public void Apply_GreaterThanOrEqual_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("greaterThanOrEqual", new JValue(userValue), new JValue(clauseValue)));
        }

        [Theory]
        [InlineData("semVerEqual", "2.0.1", "2.0.1", true)]
        [InlineData("semVerEqual", "2.0", "2.0.0", true)]
        [InlineData("semVerEqual", "2", "2.0.0", true)]
        [InlineData("semVerEqual", "2-rc1", "2.0.0-rc1", true)]
        [InlineData("semVerEqual", "2+build2", "2.0.0+build2", true)]
        [InlineData("semVerLessThan", "2.0.0", "2.0.1", true)]
        [InlineData("semVerLessThan", "2.0", "2.0.1", true)]
        [InlineData("semVerLessThan", "2.0.1", "2.0.0", false)]
        [InlineData("semVerLessThan", "2.0.1", "2.0", false)]
        [InlineData("semVerLessThan", "2.0.0-rc", "2.0.0-rc-beta", true)]
        [InlineData("semVerGreaterThan", "2.0.1", "2.0.0", true)]
        [InlineData("semVerGreaterThan", "2.0.1", "2.0", true)]
        [InlineData("semVerGreaterThan", "2.0.0", "2.0.1", false)]
        [InlineData("semVerGreaterThan", "2.0", "2.0.1", false)]
        [InlineData("semVerGreaterThan", "2.0.0-rc.1", "2.0.0-rc.0", true)]
        [InlineData("semVerLessThan", "2.0.1", "xbad%ver", false)]
        [InlineData("semVerGreaterThan", "2.0.1", "xbad%ver", false)]
        public void Apply_Any_Operators(string opName, object userValue, object clauseValue, bool expected)
        {
            var result = Operator.Apply(opName, new JValue(userValue), new JValue(clauseValue));
            Assert.Equal(expected, result);
        }
    }
}
