using System;
using LaunchDarklyClient.Extensions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace LaunchDarklyClient.Tests
{
	public class UserTests
	{
		[Test]
		public void WhenCreatingAUser_AKeyMustBeProvided()
		{
			User user = User.WithKey("AnyUniqueKey");
			Assert.AreEqual("AnyUniqueKey", user.Key);
		}

		[Test]
		public void DeserializeBasicUserAsJson()
		{
			string json = "{\"key\":\"user@test.com\"}";
			User user = JsonConvert.DeserializeObject<User>(json);
			Assert.AreEqual("user@test.com", user.Key);
		}

		[Test]
		public void DeserializeUserWithCustomAsJson()
		{
			string json = "{\"key\":\"user@test.com\", \"custom\": {\"bizzle\":\"cripps\"}}";
			User user = JsonConvert.DeserializeObject<User>(json);
			Assert.AreEqual("cripps", (string) user.Custom["bizzle"]);
		}

		[Test]
		public void SerializingAndDeserializingAUserWithCustomAttributesIsIdempotent()
		{
			User user = User.WithKey("foo@bar.com").AndCustomAttribute("bizzle", "cripps");
			string json = JsonConvert.SerializeObject(user);
			User newUser = JsonConvert.DeserializeObject<User>(json);
			Assert.AreEqual("cripps", (string) user.Custom["bizzle"]);
			Assert.AreEqual("foo@bar.com", user.Key);
			Assert.AreEqual("cripps", (string) newUser.Custom["bizzle"]);
			Assert.AreEqual("foo@bar.com", newUser.Key);
		}

		[Test]
		public void SerializingAUserWithNoAnonymousSetYieldsNoAnonymous()
		{
			User user = User.WithKey("foo@bar.com");
			string json = JsonConvert.SerializeObject(user);
			Assert.False(json.Contains("anonymous"));
		}

		[Test]
		public void WhenCreatingAUser_AnOptionalSecondaryKeyCanBeProvided()
		{
			User user = User.WithKey("AnyUniqueKey")
				.AndSecondaryKey("AnySecondaryKey");

			Assert.AreEqual("AnyUniqueKey", user.Key);
			Assert.AreEqual("AnySecondaryKey", user.SecondaryKey);
		}

		[Test]
		public void WhenCreatingAUser_AnOptionalIpAddressCanBeProvided()
		{
			User user = User.WithKey("AnyUniqueKey")
				.AndIpAddress("1.2.3.4");

			Assert.AreEqual("AnyUniqueKey", user.Key);
			Assert.AreEqual("1.2.3.4", user.IpAddress);
		}

		[Test]
		public void WhenCreatingAUser_AnOptionalCountryAddressCanBeProvided()
		{
			User user = User.WithKey("AnyUniqueKey")
				.AndCountry("US");

			Assert.AreEqual("AnyUniqueKey", user.Key);
			Assert.AreEqual("US", user.Country);
		}

		[Test]
		public void IfCountryIsSpecied_ItMustBeA2CharacterCode()
		{
			User user = User.WithKey("AnyUniqueKey");

			Assert.Throws<ArgumentException>(() => user.AndCountry(""));
			Assert.Throws<ArgumentException>(() => user.AndCountry("A"));
			Assert.Throws<ArgumentException>(() => user.AndCountry("ABC"));
		}

		[Test]
		public void WhenCreatingAUser_AnOptionalCustomAttributeCanBeAdded()
		{
			User user = User.WithKey("AnyUniqueKey")
				.AndCustomAttribute("AnyAttributeName", "AnyValue");

			Assert.AreEqual("AnyUniqueKey", user.Key);
			Assert.AreEqual("AnyValue", (string) user.Custom["AnyAttributeName"]);
		}

		[Test]
		public void WhenCreatingACustomAttribute_AnAttributeNameMustBeProvided()
		{
			User user = User.WithKey("AnyUniqueKey");
			Assert.Throws<ArgumentException>(() => user.AndCustomAttribute("", "AnyValue"));
		}

		[Test]
		public void WhenCreatingACustomAttribute_AttributeNameMustBeUnique()
		{
			User user = User.WithKey("AnyUniqueKey")
				.AndCustomAttribute("DuplicatedAttributeName", "AnyValue");

			Assert.Throws<ArgumentException>(() => user.AndCustomAttribute("DuplicatedAttributeName", "AnyValue"));
		}

		[Test]
		public void WhenCreatingAUser_MultipleCustomAttributeCanBeAdded()
		{
			User user = User.WithKey("AnyUniqueKey")
				.AndCustomAttribute("AnyAttributeName", "AnyValue")
				.AndCustomAttribute("AnyOtherAttributeName", "AnyOtherValue");

			Assert.AreEqual("AnyUniqueKey", user.Key);
			Assert.AreEqual("AnyValue", (string) user.Custom["AnyAttributeName"]);
			Assert.AreEqual("AnyOtherValue", (string) user.Custom["AnyOtherAttributeName"]);
		}

		[Test]
		public void WhenCreatingAUser_AllOptionalPropertiesCanBeSetTogether()
		{
			User user = User.WithKey("AnyUniqueKey")
				.AndIpAddress("1.2.3.4")
				.AndCountry("US")
				.AndCustomAttribute("AnyAttributeName", "AnyValue")
				.AndCustomAttribute("AnyOtherAttributeName", "AnyOtherValue");

			Assert.AreEqual("AnyUniqueKey", user.Key);
			Assert.AreEqual("1.2.3.4", user.IpAddress);
			Assert.AreEqual("US", user.Country);
			Assert.AreEqual("AnyValue", (string) user.Custom["AnyAttributeName"]);
			Assert.AreEqual("AnyOtherValue", (string) user.Custom["AnyOtherAttributeName"]);
		}
	}
}