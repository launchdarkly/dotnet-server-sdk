using LaunchDarklyClient.Extensions;
using NUnit.Framework;

namespace LaunchDarklyClient.Tests
{
	[TestFixture]
	public class LdClientTest
	{
		[Test]
		public void SecureModeHashTest()
		{
			Configuration config = Configuration.Default("secret");
			config.WithOffline(true);
			using (LdClient client = new LdClient(config))
			{
				User user = User.WithKey("Message");
				Assert.AreEqual("aa747c502a898200f9e4fa21bac68136f886a0e27aec70ba06daf2e2a5cb5597", client.SecureModeHash(user));
			}
		}
	}
}