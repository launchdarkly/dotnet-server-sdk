using System;
using LaunchDarkly.Client;
using NUnit.Framework;
using Newtonsoft.Json;

namespace LaunchDarkly.Tests
{
    public class UserTests
    {
        [Test]
        public void WhenCreatingAUser_AKeyMustBeProvided()
        {
            var user = User.WithKey("AnyUniqueKey");
            Assert.AreEqual("AnyUniqueKey", user.Key);
        }

        [Test]
        public void DeserializeBasicUserAsJson()
        {
            var json = "{\"key\":\"user@test.com\"}";
            var user = JsonConvert.DeserializeObject<User>(json);
            Assert.AreEqual("user@test.com", user.Key);
        }

        [Test]
        public void DeserializeUserWithCustomAsJson()
        {
            var json = "{\"key\":\"user@test.com\", \"custom\": {\"bizzle\":\"cripps\"}}";
            var user = JsonConvert.DeserializeObject<User>(json);
            Assert.AreEqual("cripps", (string)user.Custom["bizzle"]);
        }

        [Test]
        public void SerializingAndDeserializingAUserWithCustomAttributesIsIdempotent()
        {
            var user = User.WithKey("foo@bar.com").AndCustomAttribute("bizzle", "cripps");
            var json = JsonConvert.SerializeObject(user);
            var newUser = JsonConvert.DeserializeObject<User>(json);
            Assert.AreEqual("cripps", (string)user.Custom["bizzle"]);
            Assert.AreEqual("foo@bar.com", user.Key);
        }

        [Test]
        public void WhenCreatingAUser_AnOptionalSecondaryKeyCanBeProvided()
        {
            var user = User.WithKey("AnyUniqueKey")
                           .AndSecondaryKey("AnySecondaryKey");

            Assert.AreEqual("AnyUniqueKey", user.Key);
            Assert.AreEqual("AnySecondaryKey", user.SecondaryKey);
        }

        [Test]
        public void WhenCreatingAUser_AnOptionalIpAddressCanBeProvided()
        {
            var user = User.WithKey("AnyUniqueKey")
                           .AndIpAddress("1.2.3.4");

            Assert.AreEqual("AnyUniqueKey", user.Key);
            Assert.AreEqual("1.2.3.4", user.IpAddress);
        }

        [Test]
        public void WhenCreatingAUser_AnOptionalCountryAddressCanBeProvided()
        {
            var user = User.WithKey("AnyUniqueKey")
                           .AndCountry("US");

            Assert.AreEqual("AnyUniqueKey", user.Key);
            Assert.AreEqual("US", user.Country);
        }

        [Test]
        public void IfCountryIsSpecied_ItMustBeA2CharacterCode()
        {
            var user = User.WithKey("AnyUniqueKey");

            Assert.Throws<ArgumentException>(() => user.AndCountry(""));
            Assert.Throws<ArgumentException>(() => user.AndCountry("A"));
            Assert.Throws<ArgumentException>(() => user.AndCountry("ABC"));
        }

        [Test]
        public void WhenCreatingAUser_AnOptionalCustomAttributeCanBeAdded()
        {
            var user = User.WithKey("AnyUniqueKey")
                           .AndCustomAttribute("AnyAttributeName", "AnyValue");

            Assert.AreEqual("AnyUniqueKey", user.Key);
            Assert.AreEqual("AnyValue", (string)user.Custom["AnyAttributeName"]);
        }

        [Test]
        public void WhenCreatingACustomAttribute_AnAttributeNameMustBeProvided()
        {
            var user = User.WithKey("AnyUniqueKey");
            Assert.Throws<ArgumentException>(() => user.AndCustomAttribute("", "AnyValue"));
        }

        [Test]
        public void WhenCreatingACustomAttribute_AttributeNameMustBeUnique()
        {
            var user = User.WithKey("AnyUniqueKey")
                           .AndCustomAttribute("DuplicatedAttributeName", "AnyValue");

            Assert.Throws<ArgumentException>(() => user.AndCustomAttribute("DuplicatedAttributeName", "AnyValue"));
        }

        [Test]
        public void WhenCreatingAUser_MultipleCustomAttributeCanBeAdded()
        {
            var user = User.WithKey("AnyUniqueKey")
                           .AndCustomAttribute("AnyAttributeName", "AnyValue")
                           .AndCustomAttribute("AnyOtherAttributeName", "AnyOtherValue");

            Assert.AreEqual("AnyUniqueKey", user.Key);
            Assert.AreEqual("AnyValue", (string)user.Custom["AnyAttributeName"]);
            Assert.AreEqual("AnyOtherValue", (string)user.Custom["AnyOtherAttributeName"]);
        }


        [Test]
        public void WhenCreatingAUser_AllOptionalPropertiesCanBeSetTogether()
        {
            var user = User.WithKey("AnyUniqueKey")
                           .AndIpAddress("1.2.3.4")
                           .AndCountry("US")
                           .AndCustomAttribute("AnyAttributeName", "AnyValue")
                           .AndCustomAttribute("AnyOtherAttributeName", "AnyOtherValue");

            Assert.AreEqual("AnyUniqueKey", user.Key);
            Assert.AreEqual("1.2.3.4", user.IpAddress);
            Assert.AreEqual("US", user.Country);
            Assert.AreEqual("AnyValue", (string)user.Custom["AnyAttributeName"]);
            Assert.AreEqual("AnyOtherValue", (string)user.Custom["AnyOtherAttributeName"]);
        }


        [Test]
        public void WhenGeneratingAUserParam_ItShallBeBetween0and1()
        {
            var user = User.WithKey("AnyUniqueKey")
                           .AndSecondaryKey("1.2.3.4");

            var salt = Guid.NewGuid().ToString();
            var hash = user.GetParam(salt);

            Assert.That(hash, Is.GreaterThan(0));
            Assert.That(hash, Is.LessThan(1));
        }

        [Test]
        public void WhenGeneratingAUserParam_ItShallBeDeterministicBasedOnUserAndFeatureSalt()
        {
            var user = User.WithKey("AnyUniqueKey")
                           .AndSecondaryKey("1.2.3.4");

            var salt = Guid.NewGuid().ToString();

            var hash1 = user.GetParam(salt);
            var hash2 = user.GetParam(salt);

            Assert.AreEqual(hash1, hash2);
        }


    }
}
