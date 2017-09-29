using LaunchDarklyClient.Events;

namespace LaunchDarklyClient.Interfaces
{
	public interface IStoreEvents
	{
		void Add(Event eventToLog);
		void Flush();
	}
}