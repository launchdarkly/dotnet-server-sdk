using System;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{

    public class OperatorTest
    {
        [Fact]
        public void ExpressionValueCanBeStringOrJsonValueString()
        {
            var string1 = ExpressionValue.FromJsonValue(new JValue("x"));
            var string2 = ExpressionValue.FromString("x");
            var intValue = ExpressionValue.FromJsonValue(new JValue(1));
            Assert.True(string1.IsString);
            Assert.True(string2.IsString);
            Assert.False(intValue.IsString);
            Assert.Equal("x", string1.AsString);
            Assert.Equal("x", string2.AsString);
        }

        [Fact]
        public void ExpressionValueCanBeNumber()
        {
            var intValue = ExpressionValue.FromJsonValue(new JValue(1));
            var floatValue = ExpressionValue.FromJsonValue(new JValue(1.5f));
            var string1 = ExpressionValue.FromJsonValue(new JValue("x"));
            var string2 = ExpressionValue.FromString("x");
            Assert.True(intValue.IsNumber);
            Assert.True(floatValue.IsNumber);
            Assert.False(string1.IsNumber);
            Assert.False(string2.IsNumber);
            Assert.Equal(1.0f, intValue.AsFloat);
            Assert.Equal(1.5f, floatValue.AsFloat);
            Assert.Equal(0f, string1.AsFloat);
            Assert.Equal(0f, string2.AsFloat);
        }

        [Fact]
        public void ExpressionValueCanBeNull()
        {
            var null1 = ExpressionValue.FromString(null);
            var null2 = ExpressionValue.FromJsonValue(null);
            Assert.True(null1.IsNull);
            Assert.True(null2.IsNull);
            Assert.False(null1.IsString);
            Assert.False(null2.IsString);
            Assert.False(null1.IsNumber);
            Assert.False(null2.IsNumber);
        }

        [Fact]
        public void CanParseUtcTimestamp()
        {
            var timestamp = "1970-01-01T00:00:01Z";
            var jValueToDateTime = ExpressionValue.FromJsonValue(new JValue(timestamp)).AsDate;

            var expectedDateTime = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc);
            Assert.Equal(expectedDateTime, jValueToDateTime);
        }

        [Fact]
        public void CanParseTimestampFromTimezone()
        {
            var timestamp = "1970-01-01T00:00:00-01:00";
            var jValueToDateTime = ExpressionValue.FromJsonValue(new JValue(timestamp)).AsDate;
            var expectedDateTime = new DateTime(1970, 1, 1, 1, 0, 0, DateTimeKind.Utc);
            Assert.Equal(expectedDateTime, jValueToDateTime);
        }

        [Fact]
        public void CanParseUnixMillis()
        {
            var timestampMillis = 1000;
            var jValueToDateTime = ExpressionValue.FromJsonValue(new JValue(timestampMillis)).AsDate;
            var expectedDateTime = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc);
            Assert.Equal(expectedDateTime, jValueToDateTime);
        }

        [Fact]
        public void CanCompareTimestampsFromDifferentTimezones()
        {
            var afterTimestamp = "1970-01-01T00:00:00-01:00"; //equivalent to 1970-01-01T01:00:00Z
            var utcTimestamp = "1970-01-01T00:00:01Z";

            var after = ExpressionValue.FromJsonValue(new JValue(afterTimestamp));
            var before = ExpressionValue.FromJsonValue(new JValue(utcTimestamp));
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

            var after = ExpressionValue.FromJsonValue(new JValue(afterTimestamp));
            var before = ExpressionValue.FromJsonValue(new JValue(beforeMillis));

            Assert.True(Operator.Apply("after", after, before));
            Assert.False(Operator.Apply("after", before, after));

            Assert.True(Operator.Apply("before", before, after));
            Assert.False(Operator.Apply("before", after, before));
        }

        [Fact]
        public void Apply_UnknownOperation_ReturnsFalse()
        {
            Assert.False(Operator.Apply("unknown", ExpressionValue.FromJsonValue(new JValue(10)), ExpressionValue.FromJsonValue(new JValue(10))));
        }

        [Fact]
        public void Apply_UserValueIsNull_ReturnsFalse()
        {
            Assert.False(Operator.Apply("in", ExpressionValue.FromJsonValue(null), ExpressionValue.FromJsonValue(new JValue(10))));
        }

        [Fact]
        public void Apply_ClauseValueIsNull_ReturnsFalse()
        {
            Assert.False(Operator.Apply("in", ExpressionValue.FromJsonValue(new JValue(10)), ExpressionValue.FromJsonValue(null)));
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData("hello", "hello")]
        [InlineData(12.5d, 12.5d)]
        [InlineData(11, 11d)]
        [InlineData(11d, 11)]
        public void Apply_In_SupportedTypes_ValuesAreEqual_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("in", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData(11, 60)]
        [InlineData("first", "second")]
        [InlineData(745.4d, 92.5d)]
        [InlineData(67, 92.5d)]
        [InlineData(11.4d, 11)]
        public void Apply_In_SupportedTypes_ValuesAreNotEqual_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("in", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData("userValue", "Value")]
        [InlineData("userValue", "userValue")]
        public void Apply_EndsWith_SupportedTypes_ReturnsTrue(string userValue, string clauseValue)
        {
            Assert.True(Operator.Apply("endsWith", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_EndsWith_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("endsWith", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData("userValue", "userV")]
        [InlineData("userValue", "user")]
        public void Apply_StartsWith_SupportedTypes_ReturnsTrue(string userValue, string clauseValue)
        {
            Assert.True(Operator.Apply("startsWith", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_StartsWith_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("startsWith", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Fact]
        public void Apply_Matches_SupportedTypes_ReturnsTrue()
        {
            Assert.True(Operator.Apply("matches", ExpressionValue.FromJsonValue(new JValue("22")), ExpressionValue.FromJsonValue(new JValue(@"\d"))));
        }

        [Theory]
        [InlineData("Some text", @"\d")]
        [InlineData(@"\d", "22")]
        [InlineData("userValue", 77)]
        [InlineData(77, "userValue")]
        public void Apply_Matches_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("matches", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Fact]
        public void Apply_Contains_SupportedTypes_ReturnsTrue()
        {
            Assert.True(Operator.Apply("contains", ExpressionValue.FromJsonValue(new JValue("userValue")), ExpressionValue.FromJsonValue(new JValue("serValu"))));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_Contains_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("contains", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData(10, 11)]
        [InlineData(55d, 66)]
        [InlineData(155, 166L)]
        [InlineData(255L, 266)]
        [InlineData(355L, 366L)]
        [InlineData(455d, 466L)]
        [InlineData(555L, 566d)]
        public void Apply_LessThan_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("lessThan", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
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
        [InlineData(155, 155L)]
        [InlineData(255L, 255)]
        [InlineData(355L, 355L)]
        [InlineData(455d, 455L)]
        [InlineData(555L, 555d)]
        [InlineData(655, 654L)]
        [InlineData(755L, 754)]
        [InlineData(855L, 854L)]
        [InlineData(955d, 954L)]
        [InlineData(1055L, 1054d)]
        public void Apply_LessThan_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("lessThan", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData(99, 99)]
        [InlineData(77, 78)]
        [InlineData(46d, 46)]
        [InlineData(33d, 34d)]
        [InlineData(33d, 33d)]
        [InlineData(32, 34d)]
        [InlineData(155, 156L)]
        [InlineData(255L, 256)]
        [InlineData(355L, 356L)]
        [InlineData(455d, 456L)]
        [InlineData(555L, 556d)]
        [InlineData(655, 655L)]
        [InlineData(755L, 755)]
        [InlineData(855L, 855L)]
        [InlineData(955d, 955L)]
        [InlineData(1055L, 1055d)]
        public void Apply_LessThanOrEqual_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("lessThanOrEqual", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData(999, 99)]
        [InlineData(79, 78)]
        [InlineData(46d, 45)]
        [InlineData(33d, 32d)]
        [InlineData(23, 22d)]
        [InlineData(146, 145L)]
        [InlineData(246L, 245)]
        [InlineData(346L, 345L)]
        [InlineData(446d, 445L)]
        [InlineData(546L, 545d)]
        public void Apply_LessThanOrEqual_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("lessThanOrEqual", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData(11, 10)]
        [InlineData(66, 55d)]
        [InlineData(166, 155L)]
        [InlineData(266L, 255)]
        [InlineData(366L, 355L)]
        [InlineData(466d, 455L)]
        [InlineData(566L, 555d)]
        public void Apply_GreaterThan_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("greaterThan", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
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
        [InlineData(154, 155L)]
        [InlineData(254L, 255)]
        [InlineData(354L, 355L)]
        [InlineData(454d, 455L)]
        [InlineData(554L, 555d)]
        [InlineData(655, 655L)]
        [InlineData(755L, 755)]
        [InlineData(855L, 855L)]
        [InlineData(955d, 955L)]
        [InlineData(1055L, 1055d)]
        public void Apply_GreaterThan_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("greaterThan", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData(99, 99)]
        [InlineData(78, 77)]
        [InlineData(46, 46d)]
        [InlineData(34d, 33d)]
        [InlineData(33d, 33d)]
        [InlineData(34d, 32)]
        [InlineData(133, 133L)]
        [InlineData(233L, 233)]
        [InlineData(333L, 333L)]
        [InlineData(433d, 433L)]
        [InlineData(533L, 533d)]
        [InlineData(634, 633L)]
        [InlineData(734L, 733)]
        [InlineData(834L, 833L)]
        [InlineData(934d, 933L)]
        [InlineData(1034L, 1033d)]
        public void Apply_GreaterThanOrEqual_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("greaterThanOrEqual", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
        }

        [Theory]
        [InlineData(99, 999)]
        [InlineData(78, 79)]
        [InlineData(45, 46d)]
        [InlineData(32d, 33d)]
        [InlineData(22d, 23)]
        [InlineData(145, 146L)]
        [InlineData(245L, 246)]
        [InlineData(345L, 346L)]
        [InlineData(445d, 446L)]
        [InlineData(545L, 546d)]
        public void Apply_GreaterThanOrEqual_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("greaterThanOrEqual", ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue))));
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
        [InlineData("semVerLessThan", "2.0.0-rc", "2.0.0", true)]
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
            var result = Operator.Apply(opName, ExpressionValue.FromJsonValue(new JValue(userValue)), ExpressionValue.FromJsonValue(new JValue(clauseValue)));
            Assert.Equal(expected, result);
        }
    }
}
