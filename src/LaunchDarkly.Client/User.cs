using System;
using System.Collections.Generic;
using System.Globalization;
using LaunchDarkly.Client.Logging;

namespace LaunchDarkly.Client
{
    public class User
    {
        private static readonly ILog Logger = LogProvider.For<User>();
        
        public string Key { get; private set; }
        public string SecondaryKey { get; internal set; }
        public string IpAddress { get; internal set; }
        public string Country { get; internal set; }
        public CustomUserAttributes Custom { get; internal set; }

        private User(string key)
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
