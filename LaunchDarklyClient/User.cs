using System.Collections.Generic;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarklyClient
{
	public class User
	{
		private static readonly ILog log = LogManager.GetLogger<User>();

		public User(string key)
		{
			try
			{
				log.Trace($"Start constructor {nameof(User)}(string)");

				Key = key;
				Custom = new Dictionary<string, JToken>();
			}
			finally
			{
				log.Trace($"End constructor {nameof(User)}(string)");
			}
		}

		[JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
		public string Key {get; set;}

		[JsonProperty(PropertyName = "secondary", NullValueHandling = NullValueHandling.Ignore)]
		public string SecondaryKey {get; set;}

		[JsonProperty(PropertyName = "ip", NullValueHandling = NullValueHandling.Ignore)]
		public string IpAddress {get; set;}

		[JsonProperty(PropertyName = "country", NullValueHandling = NullValueHandling.Ignore)]
		public string Country {get; set;}

		[JsonProperty(PropertyName = "firstName", NullValueHandling = NullValueHandling.Ignore)]
		public string FirstName {get; set;}

		[JsonProperty(PropertyName = "lastName", NullValueHandling = NullValueHandling.Ignore)]
		public string LastName {get; set;}

		[JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
		public string Name {get; set;}

		[JsonProperty(PropertyName = "avatar", NullValueHandling = NullValueHandling.Ignore)]
		public string Avatar {get; set;}

		[JsonProperty(PropertyName = "email", NullValueHandling = NullValueHandling.Ignore)]
		public string Email {get; set;}

		[JsonProperty(PropertyName = "anonymous", NullValueHandling = NullValueHandling.Ignore)]
		public bool? Anonymous {get; set;}

		[JsonProperty(PropertyName = "custom", NullValueHandling = NullValueHandling.Ignore)]
		public Dictionary<string, JToken> Custom {get; set;}

		internal JToken GetValueForEvaluation(string attribute)
		{
			try
			{
				log.Trace($"Start {nameof(GetValueForEvaluation)}");

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
			finally
			{
				log.Trace($"End {nameof(GetValueForEvaluation)}");
			}
		}

		public static User WithKey(string key)
		{
			try
			{
				log.Trace($"Start {nameof(WithKey)}");

				return new User(key);
			}
			finally
			{
				log.Trace($"End {nameof(WithKey)}");
			}
		}
	}
}