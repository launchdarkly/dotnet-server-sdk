using Common.Logging;

namespace LaunchDarklyClient.Events
{
	public class IdentifyEvent : Event
	{
		private static readonly ILog log = LogManager.GetLogger<IdentifyEvent>();

		public IdentifyEvent(User user) : base("identify", user.Key, user)
		{
			try
			{
				log.Trace($"Start constructor {nameof(IdentifyEvent)}(User)");
			}
			finally
			{
				log.Trace($"End constructor {nameof(IdentifyEvent)}(User)");
			}
		}
	}
}