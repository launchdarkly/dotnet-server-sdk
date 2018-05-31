using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using LaunchDarkly.Client;

namespace LaunchDarkly.Common
{
    /// <summary>
    /// Used internally to represent user data that is being serialized in an <see cref="Event"/>.
    /// </summary>
    internal class EventUser
    {
        /// <see cref="User.Key"/>
        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public string Key { get; internal set; }

        /// <see cref="User.SecondaryKey"/>
        [JsonProperty(PropertyName = "secondary", NullValueHandling = NullValueHandling.Ignore)]
        public string SecondaryKey { get; internal set; }

        /// <see cref="User.IpAddress"/>
        [JsonProperty(PropertyName = "ip", NullValueHandling = NullValueHandling.Ignore)]
        public string IpAddress { get; internal set; }

        /// <see cref="User.Country"/>
        [JsonProperty(PropertyName = "country", NullValueHandling = NullValueHandling.Ignore)]
        public string Country { get; internal set; }

        /// <see cref="User.FirstName"/>
        [JsonProperty(PropertyName = "firstName", NullValueHandling = NullValueHandling.Ignore)]
        public string FirstName { get; internal set; }

        /// <see cref="User.LastName"/>
        [JsonProperty(PropertyName = "lastName", NullValueHandling = NullValueHandling.Ignore)]
        public string LastName { get; internal set; }

        /// <see cref="User.Name"/>
        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; internal set; }

        /// <see cref="User.Avatar"/>
        [JsonProperty(PropertyName = "avatar", NullValueHandling = NullValueHandling.Ignore)]
        public string Avatar { get; internal set; }

        /// <see cref="User.Email"/>
        [JsonProperty(PropertyName = "email", NullValueHandling = NullValueHandling.Ignore)]
        public string Email { get; internal set; }

        /// <see cref="User.Anonymous"/>
        [JsonProperty(PropertyName = "anonymous", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Anonymous { get; internal set; }

        /// <see cref="User.Custom"/>
        [JsonProperty(PropertyName = "custom", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JToken> Custom { get; internal set; }

        /// <summary>
        /// A list of attribute names that have been omitted from the event.
        /// </summary>
        [JsonProperty(PropertyName = "privateAttrs", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> PrivateAttrs { get; set; }

        internal static EventUser FromUser(User user, IBaseConfiguration config)
        {
            EventUserBuilder eub = new EventUserBuilder(user, config);
            return eub.Build();
        }
    }

    internal class EventUserBuilder
    {
        private IBaseConfiguration _config;
        private User _user;
        private EventUser _result;

        internal EventUserBuilder(User user, IBaseConfiguration config)
        {
            _user = user;
            _config = config;
            _result = new EventUser();
        }

        internal EventUser Build()
        {
            _result.Key = _user.Key;
            _result.SecondaryKey = _user.SecondaryKey;
            _result.Anonymous = _user.Anonymous;
            _result.IpAddress = CheckPrivateAttr("ip", _user.IpAddress);
            _result.Country = CheckPrivateAttr("country", _user.Country);
            _result.FirstName = CheckPrivateAttr("firstName", _user.FirstName);
            _result.LastName = CheckPrivateAttr("lastName", _user.LastName);
            _result.Name = CheckPrivateAttr("name", _user.Name);
            _result.Avatar = CheckPrivateAttr("avatar", _user.Avatar);
            _result.Email = CheckPrivateAttr("email", _user.Email);
            if (_user.Custom != null)
            {
                foreach (KeyValuePair<string, JToken> kv in _user.Custom)
                {
                    JToken value = CheckPrivateAttr(kv.Key, kv.Value);
                    if (value != null)
                    {
                        if (_result.Custom == null)
                        {
                            _result.Custom = new Dictionary<string, JToken>();
                        }
                        _result.Custom[kv.Key] = kv.Value;
                    }
                }
            }
            return _result;
        }

        private T CheckPrivateAttr<T>(string name, T value) where T: class
        {
            if (value is null)
            {
                return null;
            }
            else if (_config.AllAttributesPrivate ||
                     (_config.PrivateAttributeNames != null &&_config.PrivateAttributeNames.Contains(name)) ||
                     (_user.PrivateAttributeNames != null && _user.PrivateAttributeNames.Contains(name)))
            {
                if (_result.PrivateAttrs is null)
                {
                    _result.PrivateAttrs = new List<string>();
                }
                _result.PrivateAttrs.Add(name);
                return null;
            }
            else
            {
                return value;
            }
        }
    }
}
