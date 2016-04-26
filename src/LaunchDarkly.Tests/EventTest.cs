using LaunchDarkly.Client;
using System;
using NUnit.Framework;

namespace LaunchDarkly.Tests
{
    class EventTest
    {
        [Test]
        public void CanConvertDateTimeToUnixMillis()
        {
            var dateTime = new DateTime(2000, 1, 1, 0, 0, 10, DateTimeKind.Utc);
            var dateTimeMillis = 946684810000;
            var actualEpochMillis = Event.GetUnixTimestampMillis(dateTime);
            Assert.AreEqual(dateTimeMillis, actualEpochMillis);
        }
    }
}
