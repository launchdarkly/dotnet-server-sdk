using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// A <c>User</c> object contains specific attributes of a user browsing your site. The only mandatory
    /// property is the <c>Key</c>, which must uniquely identify each user. For authenticated users, this
    /// may be a username or e-mail address. For anonymous users, this could be an IP address or session ID.
    ///
    /// Besides the mandatory <c>Key</c>, <c>User</c> supports two kinds of optional attributes: interpreted
    /// attributes (e.g. <c>IpAddress</c> and <c>Country</c>) and custom attributes. LaunchDarkly can parse
    /// interpreted attributes and attach meaning to them. For example, from an <c>IpAddress</c>,
    /// LaunchDarkly can do a geo IP lookup and determine the user's country.
    ///
    /// Custom attributes are not parsed by LaunchDarkly. They can be used in custom rules-- for example, a
    /// custom attribute such as "customer_ranking" can be used to launch a feature to the top 10% of users
    /// on a site.
    /// </summary>
    public class User
    {
        /// <summary>
        /// The unique key for the user.
        /// </summary>
        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public string Key { get; set; }

        /// <summary>
        /// The secondary key for a user. This affects
        /// <a href="https://docs.launchdarkly.com/docs/targeting-users#section-targeting-rules-based-on-user-attributes">feature flag targeting</a>
        /// as follows: if you have chosen to bucket users by a specific attribute, the secondary key (if set)
        /// is used to further distinguish between users who are otherwise identical according to that attribute.
        /// </summary>
        [JsonProperty(PropertyName = "secondary", NullValueHandling = NullValueHandling.Ignore)]
        public string SecondaryKey { get; set; }

        /// <summary>
        /// The IP address of the user.
        /// </summary>
        [JsonProperty(PropertyName = "ip", NullValueHandling = NullValueHandling.Ignore)]
        public string IpAddress { get; set; }

        /// <summary>
        /// The 2-character country code for the user.
        /// </summary>
        [JsonProperty(PropertyName = "country", NullValueHandling = NullValueHandling.Ignore)]
        public string Country { get; set; }

        /// <summary>
        /// The user's first name.
        /// </summary>
        [JsonProperty(PropertyName = "firstName", NullValueHandling = NullValueHandling.Ignore)]
        public string FirstName { get; set; }

        /// <summary>
        /// The user's last name.
        /// </summary>
        [JsonProperty(PropertyName = "lastName", NullValueHandling = NullValueHandling.Ignore)]
        public string LastName { get; set; }

        /// <summary>
        /// The user's full name.
        /// </summary>
        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        /// <summary>
        /// The user's avatar.
        /// </summary>
        [JsonProperty(PropertyName = "avatar", NullValueHandling = NullValueHandling.Ignore)]
        public string Avatar { get; set; }

        /// <summary>
        /// The user's email address.
        /// </summary>
        [JsonProperty(PropertyName = "email", NullValueHandling = NullValueHandling.Ignore)]
        public string Email { get; set; }

        /// <summary>
        /// Whether or not the user is anonymous.
        /// </summary>
        [JsonProperty(PropertyName = "anonymous", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Anonymous { get; set; }

        /// <summary>
        /// Custom attributes for the user. These can be more conveniently set via the extension
        /// methods <c>AndCustomAttribute</c> or <c>AndPrivateCustomAttribute</c>.
        /// </summary>
        [JsonProperty(PropertyName = "custom", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JToken> Custom { get; set; }

        /// <summary>
        /// Used internally to track which attributes are private. To set private attributes,
        /// you should use extension methods such as <c>AndPrivateName</c>.
        /// </summary>
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

        /// <summary>
        /// Creates a user with the given key.
        /// </summary>
        /// <param name="key">a <c>string</c> that uniquely identifies a user</param>
        public User(string key)
        {
            Key = key;
            Custom = new Dictionary<string, JToken>();
        }

        /// <summary>
        /// Creates a user with the given key.
        /// </summary>
        /// <param name="key">a <c>string</c> that uniquely identifies a user</param>
        /// <returns>a <c>User</c> instance</returns>
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

    /// <summary>
    /// Extension methods that can be called on a <see cref="User"/> to add to its properties.
    /// </summary>
    public static class UserExtensions
    {
        /// <summary>
        /// Sets the secondary key for a user. This affects
        /// <a href="https://docs.launchdarkly.com/docs/targeting-users#section-targeting-rules-based-on-user-attributes">feature flag targeting</a>
        /// as follows: if you have chosen to bucket users by a specific attribute, the secondary key (if set)
        /// is used to further distinguish between users who are otherwise identical according to that attribute.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="secondaryKey"></param>
        /// <returns></returns>
        public static User AndSecondaryKey(this User user, string secondaryKey)
        {
            user.SecondaryKey = secondaryKey;
            return user;
        }

        /// <summary>
        /// Sets the IP for a user.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="ipAddress">the IP address for the user</param>
        /// <returns>the same user</returns>
        public static User AndIpAddress(this User user, string ipAddress)
        {
            user.IpAddress = ipAddress;
            return user;
        }

        /// <summary>
        /// Sets the IP for a user, and ensures that the IP attribute is not sent back to LaunchDarkly.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="ipAddress">the IP address for the user</param>
        /// <returns>the same user</returns>
        public static User AndPrivateIpAddress(this User user, string ipAddress)
        {
            return user.AndIpAddress(ipAddress).AddPrivate("ip");
        }

        /// <summary>
        /// Sets the country for a user. The country should be a valid
        /// <a href="http://en.wikipedia.org/wiki/ISO_3166-1">ISO 3166-1</a> alpha-2 code. If it
        /// is not a 2-character string, an <c>ArgumentException</c> will be thrown.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="country">the country code for the user</param>
        /// <returns>the same user</returns>
        public static User AndCountry(this User user, string country)
        {
            if (country.Length != 2)
                throw new ArgumentException("Country should be a 2 character ISO 3166-1 alpha-2 code. e.g. 'US'");

            user.Country = country;
            return user;
        }

        /// <summary>
        /// Sets the country for a user, and ensures that the country attribute will not be sent back
        /// to LaunchDarkly. The country should be a valid
        /// <a href="http://en.wikipedia.org/wiki/ISO_3166-1">ISO 3166-1</a> alpha-2 code. If it
        /// is not a 2-character string, an <c>ArgumentException</c> will be thrown.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="country">the country code for the user</param>
        /// <returns>the same user</returns>
        public static User AndPrivateCountry(this User user, string country)
        {
            return user.AndCountry(country).AddPrivate("country");
        }

        /// <summary>
        /// Sets the user's first name.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="firstName">the user's first name</param>
        /// <returns>the same user</returns>
        public static User AndFirstName(this User user, string firstName)
        {
            user.FirstName = firstName;
            return user;
        }

        /// <summary>
        /// Sets the user's first name, and ensures that the first name attribute will not be sent back
        /// to LaunchDarkly.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="firstName">the user's first name</param>
        /// <returns>the same user</returns>
        public static User AndPrivateFirstName(this User user, string firstName)
        {
            return user.AndFirstName(firstName).AddPrivate("firstName");
        }

        /// <summary>
        /// Sets the user's last name.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="lastName">the user's last name</param>
        /// <returns>the same user</returns>
        public static User AndLastName(this User user, string lastName)
        {
            user.LastName = lastName;
            return user;
        }

        /// <summary>
        /// Sets the user's last name, and ensures that the last name attribute will not be sent back
        /// to LaunchDarkly.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="lastName">the user's last name</param>
        /// <returns>the same user</returns>
        public static User AndPrivateLastName(this User user, string lastName)
        {
            return user.AndLastName(lastName).AddPrivate("lastName");
        }

        /// <summary>
        /// Sets the user's full name.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="name">the user's name</param>
        /// <returns>the same user</returns>
        public static User AndName(this User user, string name)
        {
            user.Name = name;
            return user;
        }

        /// <summary>
        /// Sets the user's full name, and ensures that the name attribute will not be sent back
        /// to LaunchDarkly.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="name">the user's name</param>
        /// <returns>the same user</returns>
        public static User AndPrivateName(this User user, string name)
        {
            return user.AndName(name).AddPrivate("name");
        }

        /// <summary>
        /// Sets the user's email address.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="email">the user's email</param>
        /// <returns>the same user</returns>
        public static User AndEmail(this User user, string email)
        {
            user.Email = email;
            return user;
        }

        /// <summary>
        /// Sets the user's email address, and ensures that the email attribute will not be sent back
        /// to LaunchDarkly.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="email">the user's email</param>
        /// <returns>the same user</returns>
        public static User AndPrivateEmail(this User user, string email)
        {
            return user.AndEmail(email).AddPrivate("email");
        }

        /// <summary>
        /// Sets whether this user is anonymous.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="anonymous">true if the user is anonymous</param>
        /// <returns></returns>
        public static User AndAnonymous(this User user, bool anonymous)
        {
            user.Anonymous = anonymous;
            return user;
        }

        /// <summary>
        /// Sets the user's avatar.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="avatar">the user's avatar</param>
        /// <returns>the same user</returns>
        public static User AndAvatar(this User user, string avatar)
        {
            user.Avatar = avatar;
            return user;
        }

        /// <summary>
        /// Sets the user's avatar, and ensures that the avatar attribute will not be sent back
        /// to LaunchDarkly.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="avatar">the user's avatar</param>
        /// <returns>the same user</returns>
        public static User AndPrivateAvatar(this User user, string avatar)
        {
            return user.AndAvatar(avatar).AddPrivate("avatar");
        }

        /// <summary>
        /// Adds a <c>string</c>-valued custom attribute. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndCustomAttribute(this User user, string attribute, string value)
        {
            return user.AddCustom(attribute, new JValue(value));
        }

        /// <summary>
        /// Adds a <c>bool</c>-valued custom attribute. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndCustomAttribute(this User user, string attribute, bool value)
        {
            return user.AddCustom(attribute, new JValue(value));
        }

        /// <summary>
        /// Adds an <c>int</c>-valued custom attribute. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndCustomAttribute(this User user, string attribute, int value)
        {
            return user.AddCustom(attribute, new JValue(value));
        }

        /// <summary>
        /// Adds a <c>float</c>-valued custom attribute. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndCustomAttribute(this User user, string attribute, float value)
        {
            return user.AddCustom(attribute, new JValue(value));
        }

        /// <summary>
        /// Adds a <c>long</c>-valued custom attribute. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndCustomAttribute(this User user, string attribute, long value)
        {
            return user.AddCustom(attribute, new JValue(value));
        }

        /// <summary>
        /// Adds a custom attribute whose value is a list of strings. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndCustomAttribute(this User user, string attribute, List<string> value)
        {
            return user.AddCustom(attribute, new JArray(value.ToArray()));
        }

        /// <summary>
        /// Adds a custom attribute whose value is a list of ints. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndCustomAttribute(this User user, string attribute, List<int> value)
        {
            return user.AddCustom(attribute, new JArray(value.ToArray()));
        }

        /// <summary>
        /// Adds a custom attribute whose value is a list of JSON values of any kind. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndCustomAttribute(this User user, string attribute, List<JToken> value)
        {
            return user.AddCustom(attribute, new JArray(value.ToArray()));
        }

        /// <summary>
        /// Adds a <c>string</c>-valued custom attribute, and ensures that the attribute will not
        /// be sent back to LaunchDarkly. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndPrivateCustomAttribute(this User user, string attribute, string value)
        {
            return user.AddCustom(attribute, new JValue(value)).AddPrivate(attribute);
        }

        /// <summary>
        /// Adds a <c>bool</c>-valued custom attribute, and ensures that the attribute will not
        /// be sent back to LaunchDarkly. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndPrivateCustomAttribute(this User user, string attribute, bool value)
        {
            return user.AddCustom(attribute, new JValue(value)).AddPrivate(attribute);
        }

        /// <summary>
        /// Adds an <c>int</c>-valued custom attribute, and ensures that the attribute will not
        /// be sent back to LaunchDarkly. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndPrivateCustomAttribute(this User user, string attribute, int value)
        {
            return user.AddCustom(attribute, new JValue(value)).AddPrivate(attribute);
        }

        /// <summary>
        /// Adds a <c>float</c>-valued custom attribute, and ensures that the attribute will not
        /// be sent back to LaunchDarkly. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndPrivateCustomAttribute(this User user, string attribute, float value)
        {
            return user.AddCustom(attribute, new JValue(value)).AddPrivate(attribute);
        }

        /// <summary>
        /// Adds a <c>long</c>-valued custom attribute, and ensures that the attribute will not
        /// be sent back to LaunchDarkly. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndPrivateCustomAttribute(this User user, string attribute, long value)
        {
            return user.AddCustom(attribute, new JValue(value)).AddPrivate(attribute);
        }

        /// <summary>
        /// Adds a custom attribute who value is a list of strings, and ensures that the attribute will not
        /// be sent back to LaunchDarkly. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndPrivateCustomAttribute(this User user, string attribute, List<string> value)
        {
            return user.AddCustom(attribute, new JArray(value.ToArray())).AddPrivate(attribute);
        }

        /// <summary>
        /// Adds a custom attribute who value is a list of ints, and ensures that the attribute will not
        /// be sent back to LaunchDarkly. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AndPrivateCustomAttribute(this User user, string attribute, List<int> value)
        {
            return user.AddCustom(attribute, new JArray(value.ToArray())).AddPrivate(attribute);
        }
        
        /// <summary>
        /// Adds a custom attribute whose value is a list of JSON values of any kind, and ensures that the
        /// attribute will not be sent back to LaunchDarkly. When set to one of the
        /// <a href="http://docs.launchdarkly.com/docs/targeting-users#targeting-based-on-user-attributes">built-in
        /// user attribute keys</a>, this custom attribute will be ignored.
        /// </summary>
        /// <param name="user">the user</param>
        /// <param name="attribute">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same user</returns>
        public static User AnPrivatedCustomAttribute(this User user, string attribute, List<JToken> value)
        {
            return user.AddCustom(attribute, new JArray(value.ToArray())).AddPrivate(attribute);
        }
    }
}