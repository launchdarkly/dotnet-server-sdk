using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using LaunchDarkly.Client.CustomAttributes;
using Xunit;

namespace LaunchDarkly.Tests
{

    internal class VersionConverter : ITypeConverter
    {

        public object Convert(object value, Type type)
        {
            return new Version((string) value);
        }

    }

    public class OperatorTest
    {

        private readonly Configuration _configuration = Configuration.Default("sdk-test");

        [Fact]
        public void CanCompareTimestampsFromDifferentTimezones()
        {
            var afterTimestamp = "1970-01-01T00:00:00-01:00"; //equivalent to 1970-01-01T01:00:00Z
            var timestamp = "1970-01-01T00:00:01Z";

            DateTime afterDateTime = DateTime.Parse(afterTimestamp);
            DateTime beforeDateTime = Util.ObjectToDateTime(timestamp);

            Assert.True(Operator.Apply("after", afterDateTime, beforeDateTime, _configuration));
            Assert.False(Operator.Apply("after", beforeDateTime, afterTimestamp, _configuration));

            Assert.True(Operator.Apply("before", beforeDateTime, afterTimestamp, _configuration));
            Assert.False(Operator.Apply("before", afterDateTime, beforeDateTime, _configuration));
        }

        [Fact]
        public void CanCompareTimestampWithUnixMillis()
        {
            var afterTimestamp = "1970-01-01T00:00:00-01:00"; //equivalent to 1970-01-01T01:00:00Z
            var beforeMillis = 1000;

            DateTime afterDateTime = DateTime.Parse(afterTimestamp);
            DateTime beforeDateTime = Util.ObjectToDateTime(beforeMillis);

            Assert.True(Operator.Apply("after", afterDateTime, beforeDateTime, _configuration));
            Assert.False(Operator.Apply("after", beforeDateTime, afterTimestamp, _configuration));

            Assert.True(Operator.Apply("before", beforeDateTime, afterTimestamp, _configuration));
            Assert.False(Operator.Apply("before", afterDateTime, beforeDateTime, _configuration));
        }

        [Fact]
        public void Apply_UnknownOperation_ReturnsFalse()
        {
            Assert.False(Operator.Apply("unknown", 10, 10, _configuration));
        }

        [Fact]
        public void Apply_UserValueIsNull_ReturnsFalse()
        {
            Assert.False(Operator.Apply("in", userValue: null, clauseValue: 10, configuration: _configuration));
        }

        [Fact]
        public void Apply_ClauseValueIsNull_ReturnsFalse()
        {
            Assert.False(Operator.Apply("in", userValue: 10, clauseValue: null, configuration: _configuration));
        }

        [Theory]
        [MemberData(nameof(Apply_In_ValuesAreEqual_ReturnsTrueSource))]
        public void Apply_In_ValuesAreEqual_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("in", userValue, clauseValue, _configuration));
        }

        public static IEnumerable<object[]> Apply_In_ValuesAreEqual_ReturnsTrueSource()
        {
            yield return new object[] { 10, 10 };
            yield return new object[] { "hello", "hello" };
            yield return new object[] { 12.5d, 12.5d };
            yield return new object[] { 11, 11d };
            yield return new object[] { 11d, 11 };
            yield return new object[] { new JValue(7), new JValue(7) };
            yield return new object[] { new JValue("test"), new JValue("test") };
            yield return new object[] { new Version("1.11.1"), new Version("1.11.1") };
        }

        [Fact]
        public void Apply_AnyOperator_TypeConversionIsRequiredAndNoConverterProvided_ReturnsFalse()
        {
            Clause sut = new Clause(
                attribute: "version",
                op: "in",
                values: new List<object> { "12.3.4" },
                negate: false
            );

            User user = User.WithKey("test-key")
                .AndCustomAttribute("version", new Version("12.3.4"));

            bool result = sut.MatchesUser(user, _configuration);

            Assert.False(result);
        }

        [Fact]
        public void Apply_AnyOperator_TypeConversionIsRequiredAndConverterProvided_ReturnsTrue()
        {
            Clause sut = new Clause(
                attribute: "version",
                op: "in",
                values: new List<object> { "12.3.4" },
                negate: false
            );

            User user = User.WithKey("test-key")
                .AndCustomAttribute("version", new Version("12.3.4"));

            var configuration = Configuration.Default("sdk-test")
                .WithTypeConverter(typeof(Version), new VersionConverter());

            bool result = sut.MatchesUser(user, configuration);

            Assert.True(result);
        }

        [Fact]
        public void Apply_AnyOperator_TypeConversionIsRequiredAndConverterFails_ReturnsFalse()
        {
            Clause sut = new Clause(
                attribute: "version",
                op: "in",
                values: new List<object> { 12.3 },
                negate: false
            );

            User user = User.WithKey("test-key")
                .AndCustomAttribute("version", new Version("12.3.4"));

            var configuration = Configuration.Default("sdk-test")
                .WithTypeConverter(typeof(Version), new VersionConverter());

            bool result = sut.MatchesUser(user, configuration);

            Assert.False(result);
        }

        [Theory]
        [MemberData(nameof(Apply_In_ValuesAreNotEqual_ReturnsFalseSource))]
        public void Apply_In_ValuesAreNotEqual_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("in", userValue, clauseValue, _configuration));
        }

        public static IEnumerable<object[]> Apply_In_ValuesAreNotEqual_ReturnsFalseSource()
        {
            yield return new object[] { 11, 60 };
            yield return new object[] { "first", "second" };
            yield return new object[] { 745.4d, 92.5d };
            yield return new object[] { 67, 92.5d };
            yield return new object[] { 11.4d, 11 };
            yield return new object[] { new JValue(11d), new JValue(11) };
            yield return new object[] { new JValue("test"), new JValue("te4st") };
            yield return new object[] { new Version("1.11.1"), new Version("1.11.2") };
        }

        [Theory]
        [InlineData("userValue", "Value")]
        [InlineData("userValue", "userValue")]
        public void Apply_EndsWith_ReturnsTrue(string userValue, string clauseValue)
        {
            Assert.True(Operator.Apply("endsWith", userValue, clauseValue, _configuration));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_EndsWith_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("endsWith", userValue, clauseValue, _configuration));
        }

        [Theory]
        [InlineData("userValue", "userV")]
        [InlineData("userValue", "user")]
        public void Apply_StartsWith_ReturnsTrue(string userValue, string clauseValue)
        {
            Assert.True(Operator.Apply("startsWith", userValue, clauseValue, _configuration));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_StartsWith_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("startsWith", userValue, clauseValue, _configuration));
        }

        [Fact]
        public void Apply_Matches_ReturnsTrue()
        {
            Assert.True(Operator.Apply("matches", "22", @"\d", _configuration));
        }

        [Theory]
        [InlineData("Some text", @"\d")]
        [InlineData(@"\d", "22")]
        [InlineData("userValue", 77)]
        [InlineData(77, "userValue")]
        public void Apply_Matches_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("matches", userValue, clauseValue, _configuration));
        }

        [Fact]
        public void Apply_Contains_ReturnsTrue()
        {
            Assert.True(Operator.Apply("contains", "userValue", "serValu", _configuration));
        }

        [Theory]
        [InlineData("userValue", "blah")]
        [InlineData("userValue", 77)]
        [InlineData(78, "userValue")]
        public void Apply_Contains_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("contains", userValue, clauseValue, _configuration));
        }

        [Theory]
        [MemberData(nameof(Apply_LessThan_ReturnsTrueSource))]
        public void Apply_LessThan_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("lessThan", userValue, clauseValue, _configuration));
        }

        public static IEnumerable<object[]> Apply_LessThan_ReturnsTrueSource()
        {
            yield return new object[] { 10, 11 };
            yield return new object[] { 55d, 66 };
            yield return new object[] { 55, 66d };
            yield return new object[] { new Version("1.11.1"), new Version("1.12.1") };
        }

        [Theory]
        [MemberData(nameof(Apply_LessThan_ReturnsFalseSource))]
        public void Apply_LessThan_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("lessThan", userValue, clauseValue, _configuration));
        }

        public static IEnumerable<object[]> Apply_LessThan_ReturnsFalseSource()
        {
            yield return new object[] { 10, 10 };
            yield return new object[] { 10, 9 };
            yield return new object[] { 55d, 55 };
            yield return new object[] { 55d, 54 };
            yield return new object[] { 55, 55d };
            yield return new object[] { 55, 54d };
            yield return new object[] { 55d, 55d };
            yield return new object[] { 55d, 54d };
            yield return new object[] { new Version("1.11.1"), new Version("1.11.1") };
        }

        [Theory]
        [MemberData(nameof(Apply_LessThanOrEqual_ReturnsTrueSource))]
        public void Apply_LessThanOrEqual_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("lessThanOrEqual", userValue, clauseValue, _configuration));
        }

        public static IEnumerable<object[]> Apply_LessThanOrEqual_ReturnsTrueSource()
        {
            yield return new object[] { 99, 99 };
            yield return new object[] { 77, 78 };
            yield return new object[] { 46d, 46 };
            yield return new object[] { 33d, 34d };
            yield return new object[] { 33d, 33d };
            yield return new object[] { 32, 34d };
            yield return new object[] { new Version("1.11.1"), new Version("1.11.1") };
            yield return new object[] { new Version("1.11.0"), new Version("1.11.1") };
        }

        [Theory]
        [MemberData(nameof(Apply_LessThanOrEqual_ReturnsFalseSource))]
        public void Apply_LessThanOrEqual_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("lessThanOrEqual", userValue, clauseValue, _configuration));
        }

        public static IEnumerable<object[]> Apply_LessThanOrEqual_ReturnsFalseSource()
        {
            yield return new object[] { 999, 99 };
            yield return new object[] { 79, 78 };
            yield return new object[] { 46d, 45 };
            yield return new object[] { 33d, 32d };
            yield return new object[] { 23, 22d };
            yield return new object[] { new Version("1.11.2"), new Version("1.11.1") };
            yield return new object[] { new Version("1.11.0"), new Version("1.10.1") };
        }

        [Theory]
        [MemberData(nameof(Apply_GreaterThan_ReturnsTrueSource))]
        public void Apply_GreaterThan_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("greaterThan", userValue, clauseValue, _configuration));
        }

        public static IEnumerable<object[]> Apply_GreaterThan_ReturnsTrueSource()
        {
            yield return new object[] { 11, 10 };
            yield return new object[] { 66, 55d };
            yield return new object[] { 67d, 56 };
            yield return new object[] { new Version("1.11.3"), new Version("1.11.2") };
            yield return new object[] { new Version("1.11.0"), new Version("1.10.1") };
        }

        [Theory]
        [MemberData(nameof(Apply_GreaterThan_ReturnsFalseSource))]
        public void Apply_GreaterThan_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("greaterThan", userValue, clauseValue, _configuration));
        }

        public static IEnumerable<object[]> Apply_GreaterThan_ReturnsFalseSource()
        {
            yield return new object[] { 10, 10 };
            yield return new object[] { 9, 10 };
            yield return new object[] { 55, 55d };
            yield return new object[] { 54, 55d };
            yield return new object[] { 55d, 55 };
            yield return new object[] { 54d, 55 };
            yield return new object[] { 55d, 55d };
            yield return new object[] { 54d, 55d };
            yield return new object[] { new Version("1.11.2"), new Version("1.11.3") };
            yield return new object[] { new Version("1.11.0"), new Version("1.11.0") };
        }

        [Theory]
        [MemberData(nameof(Apply_GreaterThanOrEqual_ReturnsTrueSource))]
        public void Apply_GreaterThanOrEqual_ReturnsTrue(object userValue, object clauseValue)
        {
            Assert.True(Operator.Apply("greaterThanOrEqual", userValue, clauseValue, _configuration));
        }

        public static IEnumerable<object[]> Apply_GreaterThanOrEqual_ReturnsTrueSource()
        {
            yield return new object[] { 99, 99 };
            yield return new object[] { 78, 77 };
            yield return new object[] { 46, 46d };
            yield return new object[] { 34d, 33d };
            yield return new object[] { 33d, 33d };
            yield return new object[] { 34d, 32 };
            yield return new object[] { new Version("1.11.3"), new Version("1.11.2") };
            yield return new object[] { new Version("1.11.0"), new Version("1.11.0") };
        }

        [Theory]
        [MemberData(nameof(Apply_GreaterThanOrEqual_ReturnsFalseSource))]
        public void Apply_GreaterThanOrEqual_ReturnsFalse(object userValue, object clauseValue)
        {
            Assert.False(Operator.Apply("greaterThanOrEqual", userValue, clauseValue, _configuration));
        }

        public static IEnumerable<object[]> Apply_GreaterThanOrEqual_ReturnsFalseSource()
        {
            yield return new object[] { 99, 999 };
            yield return new object[] { 78, 79 };
            yield return new object[] { 45, 46d };
            yield return new object[] { 32d, 33d };
            yield return new object[] { 22d, 23 };
            yield return new object[] { new Version("1.9.3"), new Version("1.11.2") };
            yield return new object[] { new Version("1.10.9"), new Version("1.11.0") };
        }

    }

}