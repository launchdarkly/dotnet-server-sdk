using System;
using LaunchDarkly.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class EventUserTest
    {
        static Configuration _baseConfig = new Configuration();

        static Configuration _configWithAllAttrsPrivate = new Configuration().WithAllAttributesPrivate(true);

        static Configuration _configWithSomeAttrsPrivate = new Configuration()
            .WithPrivateAttributeName("firstName")
            .WithPrivateAttributeName("bizzle");

        static User _baseUser = new User("abc")
            .AndSecondaryKey("xyz")
            .AndFirstName("Sue")
            .AndLastName("Storm")
            .AndName("Susan")
            .AndCountry("us")
            .AndAvatar("http://avatar")
            .AndIpAddress("1.2.3.4")
            .AndEmail("test@example.com")
            .AndCustomAttribute("bizzle", "def")
            .AndCustomAttribute("dizzle", "ghi");

        static User _userSpecifyingOwnPrivateAttrs = new User("abc")
            .AndSecondaryKey("xyz")
            .AndPrivateFirstName("Sue")
            .AndLastName("Storm")
            .AndName("Susan")
            .AndCountry("us")
            .AndAvatar("http://avatar")
            .AndIpAddress("1.2.3.4")
            .AndEmail("test@example.com")
            .AndPrivateCustomAttribute("bizzle", "def")
            .AndCustomAttribute("dizzle", "ghi");

        static User _anonUser = new User("abc")
            .AndAnonymous(true)
            .AndCustomAttribute("bizzle", "def")
            .AndCustomAttribute("dizzle", "ghi");

        static JObject _userWithAllAttributesJson = JObject.Parse(@"
            { ""key"": ""abc"",
              ""secondary"": ""xyz"",
              ""firstName"": ""Sue"",
              ""lastName"": ""Storm"",
              ""name"": ""Susan"",
              ""country"": ""us"",
              ""avatar"": ""http://avatar"",
              ""ip"": ""1.2.3.4"",
              ""email"": ""test@example.com"",
              ""custom"": { ""bizzle"": ""def"", ""dizzle"": ""ghi"" }
            } ");

        static JObject _userWithAllAttributesPrivateJson = JObject.Parse(@"
            { ""key"": ""abc"",
              ""secondary"": ""xyz"",
              ""custom"": { },
              ""privateAttrs"": [ ""ip"", ""country"", ""firstName"", ""lastName"",
                                  ""name"", ""avatar"", ""email"", ""bizzle"", ""dizzle"" ]
            } ");

        static JObject _userWithSomeAttributesPrivateJson = JObject.Parse(@"
            { ""key"": ""abc"",
              ""secondary"": ""xyz"",
              ""lastName"": ""Storm"",
              ""name"": ""Susan"",
              ""country"": ""us"",
              ""avatar"": ""http://avatar"",
              ""ip"": ""1.2.3.4"",
              ""email"": ""test@example.com"",
              ""custom"": { ""dizzle"": ""ghi"" },
              ""privateAttrs"": [ ""firstName"", ""bizzle"" ]
            } ");

        static JObject _anonUserWithAllAttributesPrivateJson = JObject.Parse(@"
            { ""key"": ""abc"",
              ""anonymous"": true,
              ""custom"": { },
              ""privateAttrs"": [ ""bizzle"", ""dizzle"" ]
            } ");
        
        [Fact]
        public void SerializingAUserWithNoAnonymousSetYieldsNoAnonymous()
        {
            var user = User.WithKey("foo@bar.com");
            var eu = EventUser.FromUser(user, _baseConfig);
            var json = JsonConvert.SerializeObject(eu);
            Assert.False(json.Contains("anonymous"));
        }

        [Fact]
        public void AllUserAttributesAreIncludedByDefault()
        {
            EventUser eu = EventUser.FromUser(_baseUser, _baseConfig);
            CheckJsonSerialization(eu, _userWithAllAttributesJson);
        }

        [Fact]
        public void CanHideAllAttributesExceptKeyForNonAnonUser()
        {
            EventUser eu = EventUser.FromUser(_baseUser, _configWithAllAttrsPrivate);
            CheckJsonSerialization(eu, _userWithAllAttributesPrivateJson);
        }

        [Fact]
        public void CanHideAllAttributesExceptKeyAndAnonymousForAnonUser()
        {
            EventUser eu = EventUser.FromUser(_anonUser, _configWithAllAttrsPrivate);
            CheckJsonSerialization(eu, _anonUserWithAllAttributesPrivateJson);
        }

        [Fact]
        public void CanHideSomeAttributesWithGlobalSet()
        {
            EventUser eu = EventUser.FromUser(_baseUser, _configWithSomeAttrsPrivate);
            CheckJsonSerialization(eu, _userWithSomeAttributesPrivateJson);
        }

        [Fact]
        public void CanHideSomeAttributesPerUser()
        {
            EventUser eu = EventUser.FromUser(_userSpecifyingOwnPrivateAttrs, _baseConfig);
            CheckJsonSerialization(eu, _userWithSomeAttributesPrivateJson);
        }

        private void CheckJsonSerialization(object o, JObject shouldBe)
        {
            string json = JsonConvert.SerializeObject(o);
            JObject parsed = JObject.Parse(json);
            if (!JToken.DeepEquals(shouldBe, parsed))
            {
                Console.Error.WriteLine("should be: " + shouldBe.ToString());
                Console.Error.WriteLine("was: " + parsed.ToString());
            }
            Assert.True(JToken.DeepEquals(shouldBe, parsed));
        }
    }
}
