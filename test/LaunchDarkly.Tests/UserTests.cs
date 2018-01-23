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
        public void IfCountryIsSpecified_ItMustBeA2CharacterCode()
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

        [Fact]
        public void SettingPrivateIpSetsIp()
        {
            var user = User.WithKey("key").AndPrivateIpAddress("x");
            Assert.Equal("x", user.IpAddress);
        }

        [Fact]
        public void SettingPrivateIpMarksIpAsPrivate()
        {
            var user = User.WithKey("key").AndPrivateIpAddress("x");
            Assert.True(user.PrivateAttributeNames.Contains("ip"));
        }

        [Fact]
        public void SettingPrivateEmailSetsEmail()
        {
            var user = User.WithKey("key").AndPrivateEmail("x");
            Assert.Equal("x", user.Email);
        }

        [Fact]
        public void SettingPrivateEmailMarksEmailAsPrivate()
        {
            var user = User.WithKey("key").AndPrivateEmail("x");
            Assert.True(user.PrivateAttributeNames.Contains("email"));
        }

        [Fact]
        public void SettingPrivateAvatarSetsAvatar()
        {
            var user = User.WithKey("key").AndPrivateAvatar("x");
            Assert.Equal("x", user.Avatar);
        }

        [Fact]
        public void SettingPrivateAvatarMarksAvatarAsPrivate()
        {
            var user = User.WithKey("key").AndPrivateAvatar("x");
            Assert.True(user.PrivateAttributeNames.Contains("avatar"));
        }

        [Fact]
        public void SettingPrivateFirstNameSetsFirstName()
        {
            var user = User.WithKey("key").AndPrivateFirstName("x");
            Assert.Equal("x", user.FirstName);
        }

        [Fact]
        public void SettingPrivateFirstNameMarksFirstNameAsPrivate()
        {
            var user = User.WithKey("key").AndPrivateFirstName("x");
            Assert.True(user.PrivateAttributeNames.Contains("firstName"));
        }

        [Fact]
        public void SettingPrivateLastNameSetsLastName()
        {
            var user = User.WithKey("key").AndPrivateLastName("x");
            Assert.Equal("x", user.LastName);
        }

        [Fact]
        public void SettingPrivateLastNameMarksLastNameAsPrivate()
        {
            var user = User.WithKey("key").AndPrivateLastName("x");
            Assert.True(user.PrivateAttributeNames.Contains("lastName"));
        }

        [Fact]
        public void SettingPrivateNameSetsName()
        {
            var user = User.WithKey("key").AndPrivateName("x");
            Assert.Equal("x", user.Name);
        }

        [Fact]
        public void SettingPrivateNameMarksNameAsPrivate()
        {
            var user = User.WithKey("key").AndPrivateName("x");
            Assert.True(user.PrivateAttributeNames.Contains("name"));
        }

        [Fact]
        public void SettingPrivateCountrySetsCountry()
        {
            var user = User.WithKey("key").AndPrivateCountry("us");
            Assert.Equal("us", user.Country);
        }

        [Fact]
        public void SettingPrivateCountryMarksCountryAsPrivate()
        {
            var user = User.WithKey("key").AndPrivateCountry("us");
            Assert.True(user.PrivateAttributeNames.Contains("country"));
        }
    }
}