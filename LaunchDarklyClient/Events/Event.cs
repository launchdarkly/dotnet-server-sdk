using System;
using Common.Logging;
using Newtonsoft.Json;

namespace LaunchDarklyClient.Events
{
	public abstract class Event
	{
		private static readonly ILog log = LogManager.GetLogger<Event>();

		protected Event(string kind, string key, User user)
		{
			try
			{
				log.Trace($"Start constructor {nameof(Event)}(string, string, User)");

				Kind = kind;
				CreationDate = Util.GetUnixTimestampMillis(DateTime.UtcNow);
				Key = key;
				User = user;
			}
			finally
			{
				log.Trace($"End constructor {nameof(Event)}(string, string, User)");
			}
		}

		[JsonProperty(PropertyName = "kind", NullValueHandling = NullValueHandling.Ignore)]
		public string Kind {get; private set;}

		[JsonProperty(PropertyName = "user", NullValueHandling = NullValueHandling.Ignore)]
		public User User {get; private set;}

		[JsonProperty(PropertyName = "creationDate", NullValueHandling = NullValueHandling.Ignore)]
		public long CreationDate {get; private set;}

		[JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
		public string Key {get; private set;}
	}
}