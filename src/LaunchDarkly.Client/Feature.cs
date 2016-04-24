
using System;
using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.Client.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace LaunchDarkly.Client
{
    public class Feature
    {
        private static readonly ILog Logger = LogProvider.For<Feature>();
        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public string Key { get; set; }
        [JsonProperty(PropertyName = "kind", NullValueHandling = NullValueHandling.Ignore)]
        public string Kind { get; set; }
        [JsonProperty(PropertyName = "salt", NullValueHandling = NullValueHandling.Ignore)]
        public string Salt { get; set; }
        [JsonProperty(PropertyName = "on", NullValueHandling = NullValueHandling.Ignore)]
        public bool On { get; set; }
        [JsonProperty(PropertyName = "deleted", NullValueHandling = NullValueHandling.Ignore)]
        public bool Deleted { get; set; }
        [JsonProperty(PropertyName = "variations", NullValueHandling = NullValueHandling.Ignore)]
        public List<Variation> Variations { get; set; }
        [JsonProperty(PropertyName = "version", NullValueHandling = NullValueHandling.Ignore)]
        public int Version { get; set; }

        public bool Evaluate(User user, bool defaultValue)
        {
            if (!On || user == null) return defaultValue;

            if (Variations.Any(v => v.MatchesUserTarget(user)))
                return Variations.First(v => v.MatchesUserTarget(user)).Value;

            if(Variations.Any(v=>v.Matches(user)))
                return Variations.First(v => v.Matches(user)).Value;

            var param = user.GetParam(Key, Salt);
            float sum = 0;
            foreach (var variation in Variations)
            {
                sum += (variation.Weight / 100.0f);

                if (param < sum)
                    return variation.Value;
            }

            return defaultValue;
        }
    }


    public class Variation
    {
        public bool Value { get; set; }
        public int Weight { get; set; }
        public TargetRule UserTarget { get; set; }
        public List<TargetRule> Targets { get; set; }

        public bool Matches(User user)
        {
            return Targets.Any(t => t.Matches(user));
        }
        
        public bool MatchesUserTarget(User user)
        {
           if (UserTarget != null && UserTarget.Matches(user))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }


    public class TargetRule
    {
        [JsonProperty(PropertyName = "attribute", NullValueHandling = NullValueHandling.Ignore)]
        public string Attribute { get; set; }
        [JsonProperty(PropertyName = "op", NullValueHandling = NullValueHandling.Ignore)]
        public string Op { get; set; }
        [JsonProperty(PropertyName = "values", NullValueHandling = NullValueHandling.Ignore)]
        public List<Object> Values { get; set; }

        public bool Matches(User user)
        {
            var userValue = GetUserValue(user);

            if (!(userValue is string) && typeof(IEnumerable).IsAssignableFrom(userValue.GetType()))
            {
                var uvs = (IEnumerable<object>)userValue;
                return Values.Intersect<object>(uvs).Any();
            }

            return Values.Contains(userValue);
        }

        private Object GetUserValue(User user)
        {
            switch (Attribute)
            {
                case "key":
                    return user.Key;
                case "ip":
                    return user.IpAddress;
                case "country":
                    return user.Country;
                case "firstName":
                    return user.FirstName;
                case "lastName":
                    return user.LastName;
                case "avatar":
                    return user.Avatar;
                case "anonymous":
                    return user.Anonymous;
                case "name":
                    return user.Name;
                case "email":
                    return user.Email;
                default:
                    var token = user.Custom[Attribute];
                    if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                    {
                        var arr = (JArray)token;
                        return arr.Values<JToken>().Select(i => ((JValue)i).Value);                    
                    }
                    else if (token.Type == JTokenType.Object)
                    {
                        throw new ArgumentException(string.Format("Rule contains nested custom object for attribute '{0}'"), Attribute);
                    }
                    else
                    {
                        var val = (JValue)token;
                        return val.Value;                        
                    }
            }
        }
    }
}
