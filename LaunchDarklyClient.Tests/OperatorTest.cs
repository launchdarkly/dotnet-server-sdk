using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LaunchDarklyClient.Tests
{
	[TestFixture]
	public class OperatorTest
	{
		[Test]
		public void CanParseUtcTimestamp()
		{
			const string timestamp = "1970-01-01T00:00:01Z";
			DateTime? jValueToDateTime = Operator.JValueToDateTime(new JValue(timestamp));

			DateTime expectedDateTime = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc);
			Assert.AreEqual(expectedDateTime, jValueToDateTime);
		}

		[Test]
		public void CanParseTimestampFromTimezone()
		{
			const string timestamp = "1970-01-01T00:00:00-01:00";
			DateTime? jValueToDateTime = Operator.JValueToDateTime(new JValue(timestamp));
			DateTime expectedDateTime = new DateTime(1970, 1, 1, 1, 0, 0, DateTimeKind.Utc);
			Assert.AreEqual(expectedDateTime, jValueToDateTime);
		}

		[Test]
		public void CanParseUnixMillis()
		{
			const int timestampMillis = 1000;
			DateTime? jValueToDateTime = Operator.JValueToDateTime(new JValue(timestampMillis));
			DateTime expectedDateTime = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc);
			Assert.AreEqual(expectedDateTime, jValueToDateTime);
		}

		[Test]
		public void CanCompareTimestampsFromDifferentTimezones()
		{
			const string afterTimestamp = "1970-01-01T00:00:00-01:00"; //equivalent to 1970-01-01T01:00:00Z
			const string utcTimestamp = "1970-01-01T00:00:01Z";

			JValue after = new JValue(afterTimestamp);
			JValue before = new JValue(utcTimestamp);
			Assert.True(Operator.Apply("after", after, before));
			Assert.False(Operator.Apply("after", before, after));

			Assert.True(Operator.Apply("before", before, after));
			Assert.False(Operator.Apply("before", after, before));
		}

		[Test]
		public void CanCompareTimestampWithUnixMillis()
		{
			const string afterTimestamp = "1970-01-01T00:00:00-01:00"; //equivalent to 1970-01-01T01:00:00Z
			const int beforeMillis = 1000;

			JValue after = new JValue(afterTimestamp);
			JValue before = new JValue(beforeMillis);

			Assert.True(Operator.Apply("after", after, before));
			Assert.False(Operator.Apply("after", before, after));

			Assert.True(Operator.Apply("before", before, after));
			Assert.False(Operator.Apply("before", after, before));
		}

		[Test]
		public void Apply_UnknownOperation_ReturnsFalse()
		{
			Assert.False(Operator.Apply("unknown", new JValue(10), new JValue(10)));
		}

		[Test]
		public void Apply_UserValueIsNull_ReturnsFalse()
		{
			Assert.False(Operator.Apply("in", null, new JValue(10)));
		}

		[Test]
		public void Apply_ClauseValueIsNull_ReturnsFalse()
		{
			Assert.False(Operator.Apply("in", new JValue(10), null));
		}

		[Theory]
		[TestCase(10, 10)]
		[TestCase("hello", "hello")]
		[TestCase(12.5d, 12.5d)]
		[TestCase(11, 11d)]
		[TestCase(11d, 11)]
		public void Apply_In_SupportedTypes_ValuesAreEqual_ReturnsTrue(object userValue, object clauseValue)
		{
			Assert.True(Operator.Apply("in", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase(11, 60)]
		[TestCase("first", "second")]
		[TestCase(745.4d, 92.5d)]
		[TestCase(67, 92.5d)]
		[TestCase(11.4d, 11)]
		public void Apply_In_SupportedTypes_ValuesAreNotEqual_ReturnsFalse(object userValue, object clauseValue)
		{
			Assert.False(Operator.Apply("in", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase("userValue", "Value")]
		[TestCase("userValue", "userValue")]
		public void Apply_EndsWith_SupportedTypes_ReturnsTrue(string userValue, string clauseValue)
		{
			Assert.True(Operator.Apply("endsWith", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase("userValue", "blah")]
		[TestCase("userValue", 77)]
		[TestCase(78, "userValue")]
		public void Apply_EndsWith_ReturnsFalse(object userValue, object clauseValue)
		{
			Assert.False(Operator.Apply("endsWith", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase("userValue", "userV")]
		[TestCase("userValue", "user")]
		public void Apply_StartsWith_SupportedTypes_ReturnsTrue(string userValue, string clauseValue)
		{
			Assert.True(Operator.Apply("startsWith", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase("userValue", "blah")]
		[TestCase("userValue", 77)]
		[TestCase(78, "userValue")]
		public void Apply_StartsWith_ReturnsFalse(object userValue, object clauseValue)
		{
			Assert.False(Operator.Apply("startsWith", new JValue(userValue), new JValue(clauseValue)));
		}

		[Test]
		public void Apply_Matches_SupportedTypes_ReturnsTrue()
		{
			Assert.True(Operator.Apply("matches", new JValue("22"), new JValue(@"\d")));
		}

		[Theory]
		[TestCase("Some text", @"\d")]
		[TestCase(@"\d", "22")]
		[TestCase("userValue", 77)]
		[TestCase(77, "userValue")]
		public void Apply_Matches_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
		{
			Assert.False(Operator.Apply("matches", new JValue(userValue), new JValue(clauseValue)));
		}

		[Test]
		public void Apply_Contains_SupportedTypes_ReturnsTrue()
		{
			Assert.True(Operator.Apply("contains", new JValue("userValue"), new JValue("serValu")));
		}

		[Theory]
		[TestCase("userValue", "blah")]
		[TestCase("userValue", 77)]
		[TestCase(78, "userValue")]
		public void Apply_Contains_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
		{
			Assert.False(Operator.Apply("contains", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase(10, 11)]
		[TestCase(55d, 66)]
		public void Apply_LessThan_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
		{
			Assert.True(Operator.Apply("lessThan", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase(10, 10)]
		[TestCase(10, 9)]
		[TestCase(55d, 55)]
		[TestCase(55d, 54)]
		[TestCase(55, 55d)]
		[TestCase(55, 54d)]
		[TestCase(55d, 55d)]
		[TestCase(55d, 54d)]
		public void Apply_LessThan_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
		{
			Assert.False(Operator.Apply("lessThan", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase(99, 99)]
		[TestCase(77, 78)]
		[TestCase(46d, 46)]
		[TestCase(33d, 34d)]
		[TestCase(33d, 33d)]
		[TestCase(32, 34d)]
		public void Apply_LessThanOrEqual_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
		{
			Assert.True(Operator.Apply("lessThanOrEqual", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase(999, 99)]
		[TestCase(79, 78)]
		[TestCase(46d, 45)]
		[TestCase(33d, 32d)]
		[TestCase(23, 22d)]
		public void Apply_LessThanOrEqual_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
		{
			Assert.False(Operator.Apply("lessThanOrEqual", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase(11, 10)]
		[TestCase(66, 55d)]
		public void Apply_GreaterThan_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
		{
			Assert.True(Operator.Apply("greaterThan", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase(10, 10)]
		[TestCase(9, 10)]
		[TestCase(55, 55d)]
		[TestCase(54, 55d)]
		[TestCase(55d, 55)]
		[TestCase(54d, 55)]
		[TestCase(55d, 55d)]
		[TestCase(54d, 55d)]
		public void Apply_GreaterThan_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
		{
			Assert.False(Operator.Apply("greaterThan", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase(99, 99)]
		[TestCase(78, 77)]
		[TestCase(46, 46d)]
		[TestCase(34d, 33d)]
		[TestCase(33d, 33d)]
		[TestCase(34d, 32)]
		public void Apply_GreaterThanOrEqual_SupportedTypes_ReturnsTrue(object userValue, object clauseValue)
		{
			Assert.True(Operator.Apply("greaterThanOrEqual", new JValue(userValue), new JValue(clauseValue)));
		}

		[Theory]
		[TestCase(99, 999)]
		[TestCase(78, 79)]
		[TestCase(45, 46d)]
		[TestCase(32d, 33d)]
		[TestCase(22d, 23)]
		public void Apply_GreaterThanOrEqual_SupportedTypes_ReturnsFalse(object userValue, object clauseValue)
		{
			Assert.False(Operator.Apply("greaterThanOrEqual", new JValue(userValue), new JValue(clauseValue)));
		}
	}
}