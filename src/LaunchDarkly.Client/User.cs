using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    public class User
    {
        public string Key { get; set; }

        public string SecondaryKey { get; set; }

        public string IpAddress { get; set; }

        public string Country { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Name { get; set; }

        public string Avatar { get; set; }

        public string Email { get; set; }

        public bool? Anonymous { get; set; }

        public Dictionary<string, JToken> Custom { get; set; }

        public ISet<string> PrivateAttributeNames { get; set; }

        internal JToken GetValueForEvaluation(string attribute)
        {
            switch (attribute)
            {
                case "key":
                    return new JValue(Key);
                case "secondary":
                    return null;
                case "ip":
                    return new JValue(IpAddress);
                case "email":
                    return new JValue(Email);
                case "avatar":
                    return new JValue(Avatar);
                case "firstName":
                    return new JValue(FirstName);
                case "lastName":
                    return new JValue(LastName);
                case "name":
                    return new JValue(Name);
                case "country":
                    return new JValue(Country);
                case "anonymous":
                    return new JValue(Anonymous);
                default:
                    JToken customValue;
                    Custom.TryGetValue(attribute, out customValue);
                    return customValue;
            }
        }

        public User(string key)
        {
            Key = key;
            Custom = new Dictionary<string, JToken>();
        }

        public static User WithKey(string key)
        {
            return new User(key);
        }

        internal User AddCustom(string attribute, JToken value)
        {
            if (attribute == string.Empty)
            {
                throw new ArgumentException("Attribute Name cannot be empty");
            }
            if (Custom is null)
            {
                Custom = new Dictionary<string, JToken>();
            }
            Custom.Add(attribute, value);
            return this;
        }

        internal User AddPrivate(string name)
        {
            if (PrivateAttributeNames is null)
            {
                PrivateAttributeNames = new HashSet<string>();
            }
            PrivateAttributeNames.Add(name);
            return this;
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

        public static User AndPrivateIpAddress(this User user, string ipAddress)
        {
            return user.AndIpAddress(ipAddress).AddPrivate("ip");
        }

        public static User AndCountry(this User user, string country)
        {
            if (country.Length != 2)
                throw new ArgumentException("Country should be a 2 character ISO 3166-1 alpha-2 code. e.g. 'US'");

            user.Country = country;
            return user;
        }

        public static User AndPrivateCountry(this User user, string country)
        {
            return user.AndCountry(country).AddPrivate("country");
        }

        public static User AndFirstName(this User user, string firstName)
        {
            user.FirstName = firstName;
            return user;
        }

        public static User AndPrivateFirstName(this User user, string firstName)
        {
            return user.AndFirstName(firstName).AddPrivate("firstName");
        }

        public static User AndLastName(this User user, string lastName)
        {
            user.LastName = lastName;
            return user;
        }

        public static User AndPrivateLastName(this User user, string lastName)
        {
            return user.AndLastName(lastName).AddPrivate("lastName");
        }

        public static User AndName(this User user, string name)
        {
            user.Name = name;
            return user;
        }

        public static User AndPrivateName(this User user, string name)
        {
            return user.AndName(name).AddPrivate("name");
        }

        public static User AndEmail(this User user, string email)
        {
            user.Email = email;
            return user;
        }

        public static User AndPrivateEmail(this User user, string email)
        {
            return user.AndEmail(email).AddPrivate("email");
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

        public static User AndPrivateAvatar(this User user, string avatar)
        {
            return user.AndAvatar(avatar).AddPrivate("avatar");
        }

        public static User AndCustomAttribute(this User user, string attribute, string value)
        {
            return user.AddCustom(attribute, new JValue(value));
        }

        public static User AndCustomAttribute(this User user, string attribute, bool value)
        {
            return user.AddCustom(attribute, new JValue(value));
        }

        public static User AndCustomAttribute(this User user, string attribute, int value)
        {
            return user.AddCustom(attribute, new JValue(value));
        }

        public static User AndCustomAttribute(this User user, string attribute, float value)
        {
            return user.AddCustom(attribute, new JValue(value));
        }

        public static User AndCustomAttribute(this User user, string attribute, List<string> value)
        {
            return user.AddCustom(attribute, new JArray(value.ToArray()));
        }

        public static User AndCustomAttribute(this User user, string attribute, List<int> value)
        {
            return user.AddCustom(attribute, new JArray(value.ToArray()));
        }

        public static User AndPrivateCustomAttribute(this User user, string attribute, string value)
        {
            return user.AddCustom(attribute, new JValue(value)).AddPrivate(attribute);
        }

        public static User AndPrivateCustomAttribute(this User user, string attribute, bool value)
        {
            return user.AddCustom(attribute, new JValue(value)).AddPrivate(attribute);
        }

        public static User AndPrivateCustomAttribute(this User user, string attribute, int value)
        {
            return user.AddCustom(attribute, new JValue(value)).AddPrivate(attribute);
        }

        public static User AndPrivateCustomAttribute(this User user, string attribute, float value)
        {
            return user.AddCustom(attribute, new JValue(value)).AddPrivate(attribute);
        }

        public static User AndPrivateCustomAttribute(this User user, string attribute, List<string> value)
        {
            return user.AddCustom(attribute, new JArray(value.ToArray())).AddPrivate(attribute);
        }

        public static User AndPrivateCustomAttribute(this User user, string attribute, List<int> value)
        {
            return user.AddCustom(attribute, new JArray(value.ToArray())).AddPrivate(attribute);
        }
    }
}