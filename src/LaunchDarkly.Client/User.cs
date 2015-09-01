using System;
using System.Collections.Generic;
using System.Globalization;
using LaunchDarkly.Client.Logging;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    public class User
    {
        private static readonly ILog Logger = LogProvider.For<User>();

        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public string Key { get; set; }
        [JsonProperty(PropertyName = "secondary", NullValueHandling = NullValueHandling.Ignore)]
        public string SecondaryKey { get; set; }
        [JsonProperty(PropertyName = "ip", NullValueHandling = NullValueHandling.Ignore)]
        public string IpAddress { get; set; }
        [JsonProperty(PropertyName = "country", NullValueHandling = NullValueHandling.Ignore)]
        public string Country { get; set; }
        [JsonProperty(PropertyName = "custom", NullValueHandling = NullValueHandling.Ignore)]
        public CustomUserAttributes Custom { get; set; }
        [JsonProperty(PropertyName = "firstName", NullValueHandling = NullValueHandling.Ignore)]
        public string FirstName { get; set; }
        [JsonProperty(PropertyName = "lastName", NullValueHandling = NullValueHandling.Ignore)]
        public string LastName { get; set; }
        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "avatar", NullValueHandling = NullValueHandling.Ignore)]
        public string Avatar { get; set; }
        [JsonProperty(PropertyName = "email", NullValueHandling = NullValueHandling.Ignore)]
        public string Email { get; set; }
        [JsonProperty(PropertyName ="anonymous", NullValueHandling = NullValueHandling.Ignore)]
        public bool Anonymous { get; set; }

        public User(string key)
        {
            Key = key;
            Custom = new CustomUserAttributes();
        }

        public static User WithKey(string key)
        {
            return new User(key);
        }

        public float GetParam(string salt)
        {
            var idHash = Key;

            if (!string.IsNullOrEmpty(SecondaryKey))
                idHash += "." + SecondaryKey; 

            var hash = ShaHex.Hash(string.Format("{0}.{1}.{2}", Key, salt, idHash)).Substring(0, 15);

            var longValue = long.Parse(hash, NumberStyles.HexNumber);
            const float longScale = 0xFFFFFFFFFFFFFFFL;

            return longValue/longScale;
        }

    }

    public static class UserExtensions
    {
        public static User AndSecondaryKey(this User user, string secondaryKey)
        {
            user.SecondaryKey = secondaryKey;
            return user;
        }

        public static User AndIpAddress(this User user, string ipAddress)
        {
            user.IpAddress = ipAddress;
            return user;
        }

        public static User AndCountry(this User user, string country)
        {
            if (country.Length != 2)
                throw new ArgumentException("Country should be a 2 character ISO 3166-1 alpha-2 code. e.g. 'US'");

            user.Country = country;
            return user;
        }

        public static User AndFirstName(this User user, string firstName)
        {
            user.FirstName = firstName;
            return user;
        }

        public static User AndLastName(this User user, string lastName)
        {
            user.LastName = lastName;
            return user;
        }

        public static User AndName(this User user, string name)
        {
            user.LastName = name;
            return user;
        }

        public static User AndEmail(this User user, string email)
        {
            user.Email = email;
            return user;
        }

        public static User AndAnonymous(this User user, bool anonymous)
        {
            user.Anonymous = anonymous;
            return user;
        }

        public static User AndAvatar(this User user, string avatar)
        {
            user.Avatar = avatar;
            return user;
        }

        public static User AndCustomAttribute(this User user, string attribute, string value)
        {
            if (attribute == string.Empty)
                throw new ArgumentException("Attribute Name can not be empty");

            if (user.Custom.Contains(attribute))
                throw new ArgumentException("Attribute Name must be unique");

            user.Custom.Add(attribute, new List<string> { value });
            return user;
        }
    }


    public class CustomUserAttributes
    {
        private readonly Dictionary<string, List<string>> _attributes;

        public string this[string attribute]
        {
            get { return _attributes[attribute][0]; }
        }

        public void Add(string attribute, List<string> value)
        {
            _attributes.Add(attribute, value);
        }

        public bool Contains(string attribute)
        {
            return _attributes.ContainsKey(attribute);
        }

        internal CustomUserAttributes()
        {
            _attributes = new Dictionary<string, List<string>>();
        }
    }
}
