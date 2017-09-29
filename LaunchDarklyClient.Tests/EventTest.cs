using System;
using NUnit.Framework;

namespace LaunchDarklyClient.Tests
{
	[TestFixture]
	public class EventTest
	{
		[Test]
		public void CanConvertDateTimeToUnixMillis()
		{
			DateTime dateTime = new DateTime(2000, 1, 1, 0, 0, 10, DateTimeKind.Utc);
			long dateTimeMillis = 946684810000;
			long actualEpochMillis = Util.GetUnixTimestampMillis(dateTime);
			Assert.AreEqual(dateTimeMillis, actualEpochMillis);
		}
	}
}