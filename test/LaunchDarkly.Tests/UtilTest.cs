using LaunchDarkly.Client;
using System;
using Xunit;

namespace LaunchDarkly.Tests
{

    public class UtilTest
    {

        [Fact]
        public void CanConvertDateTimeToUnixMillis()
        {
            var dateTime = new DateTime(2000, 1, 1, 0, 0, 10, DateTimeKind.Utc);
            var dateTimeMillis = 946684810000;
            var actualEpochMillis = Util.GetUnixTimestampMillis(dateTime);
            Assert.Equal(dateTimeMillis, actualEpochMillis);
        }

        [Fact]
        public void CanParseUtcTimestamp()
        {
            var timestamp = "1970-01-01T00:00:01Z";
            var jValueToDateTime = Util.ObjectToDateTime(timestamp);

            var expectedDateTime = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc);
            Assert.Equal(expectedDateTime, jValueToDateTime);
        }

        [Fact]
        public void CanParseTimestampFromTimezone()
        {
            var timestamp = "1970-01-01T00:00:00-01:00";
            var jValueToDateTime = Util.ObjectToDateTime(timestamp);
            var expectedDateTime = new DateTime(1970, 1, 1, 1, 0, 0, DateTimeKind.Utc);
            Assert.Equal(expectedDateTime, jValueToDateTime);
        }

        [Fact]
        public void CanParseUnixMillis()
        {
            var timestampMillis = 1000;
            var jValueToDateTime = Util.ObjectToDateTime(timestampMillis);
            var expectedDateTime = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc);
            Assert.Equal(expectedDateTime, jValueToDateTime);
        }

    }

}