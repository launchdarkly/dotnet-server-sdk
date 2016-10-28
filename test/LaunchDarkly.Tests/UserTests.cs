using System;
using LaunchDarkly.Client;
using Newtonsoft.Json;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class UserTests
    {
        [Fact]
        public void WhenCreatingAUser_AKeyMustBeProvided()
        {
            var user = User.WithKey("AnyUniqueKey");
            Assert.Equal("AnyUniqueKey", user.Key);
        }

        [Fact]
        public void DeserializeBasicUserAsJson()
        {
            var json = "{\"key\":\"user@test.com\"}";
            var user = JsonConvert.DeserializeObject<User>(json);
            Assert.Equal("user@test.com", user.Key);
        }

        [Fact]
        public void DeserializeUserWithCustomAsJson()
        {
            var json = "{\"key\":\"user@test.com\", \"custom\": {\"bizzle\":\"cripps\"}}";
            var user = JsonConvert.DeserializeObject<User>(json);
            Assert.Equal("cripps", (string) user.Custom["bizzle"]);
        }

        [Fact]
        public void SerializingAndDeserializingAUserWithCustomAttributesIsIdempotent()
        {
            var user = User.WithKey("foo@bar.com").AndCustomAttribute("bizzle", "cripps");
            var json = JsonConvert.SerializeObject(user);
            var newUser = JsonConvert.DeserializeObject<User>(json);
            Assert.Equal("cripps", (string) user.Custom["bizzle"]);
            Assert.Equal("foo@bar.com", user.Key);
        }

        [Fact]
        public void SerializingAUserWithNoAnonymousSetYieldsNoAnonymous()
        {
            var user = User.WithKey("foo@bar.com");
            var json = JsonConvert.SerializeObject(user);
            Assert.False(json.Contains("anonymous"));
        }

        [Fact]
        public void WhenCreatingAUser_AnOptionalSecondaryKeyCanBeProvided()
        {
            var user = User.WithKey("AnyUniqueKey")
                .AndSecondaryKey("AnySecondaryKey");

            Assert.Equal("AnyUniqueKey", user.Key);
            Assert.Equal("AnySecondaryKey", user.SecondaryKey);
        }

        [Fact]
        public void WhenCreatingAUser_AnOptionalIpAddressCanBeProvided()
        {
            var user = User.WithKey("AnyUniqueKey")
                .AndIpAddress("1.2.3.4");

            Assert.Equal("AnyUniqueKey", user.Key);
            Assert.Equal("1.2.3.4", user.IpAddress);
        }

        [Fact]
        public void WhenCreatingAUser_AnOptionalCountryAddressCanBeProvided()
        {
            var user = User.WithKey("AnyUniqueKey")
                .AndCountry("US");

            Assert.Equal("AnyUniqueKey", user.Key);
            Assert.Equal("US", user.Country);
        }

        [Fact]
        public void IfCountryIsSpecied_ItMustBeA2CharacterCode()
        {
            var user = User.WithKey("AnyUniqueKey");

            Assert.Throws<ArgumentException>(() => user.AndCountry(""));
            Assert.Throws<ArgumentException>(() => user.AndCountry("A"));
            Assert.Throws<ArgumentException>(() => user.AndCountry("ABC"));
        }

        [Fact]
        public void WhenCreatingAUser_AnOptionalCustomAttributeCanBeAdded()
        {
            var user = User.WithKey("AnyUniqueKey")
                .AndCustomAttribute("AnyAttributeName", "AnyValue");

            Assert.Equal("AnyUniqueKey", user.Key);
            Assert.Equal("AnyValue", (string) user.Custom["AnyAttributeName"]);
        }

        [Fact]
        public void WhenCreatingACustomAttribute_AnAttributeNameMustBeProvided()
        {
            var user = User.WithKey("AnyUniqueKey");
            Assert.Throws<ArgumentException>(() => user.AndCustomAttribute("", "AnyValue"));
        }

        [Fact]
        public void WhenCreatingACustomAttribute_AttributeNameMustBeUnique()
        {
            var user = User.WithKey("AnyUniqueKey")
                .AndCustomAttribute("DuplicatedAttributeName", "AnyValue");

            Assert.Throws<ArgumentException>(() => user.AndCustomAttribute("DuplicatedAttributeName", "AnyValue"));
        }

        [Fact]
        public void WhenCreatingAUser_MultipleCustomAttributeCanBeAdded()
        {
            var user = User.WithKey("AnyUniqueKey")
                .AndCustomAttribute("AnyAttributeName", "AnyValue")
                .AndCustomAttribute("AnyOtherAttributeName", "AnyOtherValue");

            Assert.Equal("AnyUniqueKey", user.Key);
            Assert.Equal("AnyValue", (string) user.Custom["AnyAttributeName"]);
            Assert.Equal("AnyOtherValue", (string) user.Custom["AnyOtherAttributeName"]);
        }


        [Fact]
        public void WhenCreatingAUser_AllOptionalPropertiesCanBeSetTogether()
        {
            var user = User.WithKey("AnyUniqueKey")
                .AndIpAddress("1.2.3.4")
                .AndCountry("US")
                .AndCustomAttribute("AnyAttributeName", "AnyValue")
                .AndCustomAttribute("AnyOtherAttributeName", "AnyOtherValue");

            Assert.Equal("AnyUniqueKey", user.Key);
            Assert.Equal("1.2.3.4", user.IpAddress);
            Assert.Equal("US", user.Country);
            Assert.Equal("AnyValue", (string) user.Custom["AnyAttributeName"]);
            Assert.Equal("AnyOtherValue", (string) user.Custom["AnyOtherAttributeName"]);
        }
    }
}