using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarklyClient.Events
{
	public class FeatureRequestEvent : Event
	{
		private static readonly ILog log = LogManager.GetLogger<FeatureRequestEvent>();

		public FeatureRequestEvent(string key, User user, JToken value, JToken defaultValue, JToken version, JToken prereqOf) : base("feature", key, user)
		{
			try
			{
				log.Trace($"Start constructor {nameof(FeatureRequestEvent)}(string, User, JToken, JToken, JToken, JToken)");

				Value = value;
				Default = defaultValue;
				Version = version;
				PrereqOf = prereqOf;
			}
			finally
			{
				log.Trace($"End constructor {nameof(FeatureRequestEvent)}(string, User, JToken, JToken, JToken, JToken)");
			}
		}

		[JsonProperty(PropertyName = "value", NullValueHandling = NullValueHandling.Ignore)]
		public JToken Value {get; private set;}

		[JsonProperty(PropertyName = "default", NullValueHandling = NullValueHandling.Ignore)]
		public JToken Default {get; private set;}

		[JsonProperty(PropertyName = "version", NullValueHandling = NullValueHandling.Ignore)]
		public JToken Version {get; private set;}

		[JsonProperty(PropertyName = "prereqOf", NullValueHandling = NullValueHandling.Ignore)]
		public JToken PrereqOf {get; private set;}
	}
}