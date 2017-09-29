using Common.Logging;
using Newtonsoft.Json;

namespace LaunchDarklyClient.Events
{
	public class CustomEvent : Event
	{
		private static readonly ILog log = LogManager.GetLogger<CustomEvent>();

		public CustomEvent(string key, User user, string data) : base("custom", key, user)
		{
			try
			{
				log.Trace($"Start constructor {nameof(CustomEvent)}(string, User, string");

				Data = data;
			}
			finally
			{
				log.Trace($"End constructor {nameof(CustomEvent)}(string, User, string");
			}
		}

		[JsonProperty(PropertyName = "data", NullValueHandling = NullValueHandling.Ignore)]
		public string Data {get; private set;}
	}
}