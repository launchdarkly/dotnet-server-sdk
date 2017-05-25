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
    }
}