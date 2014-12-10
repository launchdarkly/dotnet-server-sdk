
using System;
using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.Client.Logging;

namespace LaunchDarkly.Client
{
    public class Feature
    {
        private static readonly ILog Logger = LogProvider.For<Feature>();
        
        public string Name { get; set; }
        public string Key { get; set; }
        public string Kind { get; set; }
        public string Salt { get; set; }
        public bool On { get; set; }
        public List<Variation> Variations { get; set; }
        public int Ttl { get; set; }
        public bool IncludeInSnippet { get; set; }
        public string CommitDate { get; set; }
        public string CreationDate { get; set; }
        public int Version { get; set; }
        public string Sel { get; set; }

        public bool Evaluate(User user, bool defaultValue)
        {
            if (!On) return defaultValue;

            if(Variations.Any(v=>v.Matches(user)))
                return Variations.First(v => v.Matches(user)).Value;

            var param = user.GetParam(Salt);
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
        public List<TargetRule> Targets { get; set; }

        public bool Matches(User user)
        {
            return Targets.Any(t => t.Matches(user));
        }
    }


    public class TargetRule
    {
        public string Attribute { get; set; }
        public string Op { get; set; }
        public List<string> Values { get; set; }

        public bool Matches(User user)
        {
            var userValue = GetUserValue(user);
            return Values.Contains(userValue);
        }

        private string GetUserValue(User user)
        {
            switch (Attribute)
            {
                case "key":
                    return user.Key;
                case "ip":
                    return user.IpAddress;
                case "country":
                    return user.Country;
                case "custom":
                    throw new NotImplementedException("Custom Attributes");
                default:
                    throw new ArgumentException(string.Format("Rule uses unknown Attribute '{0}'", Attribute));
            }
        }
    }
}
