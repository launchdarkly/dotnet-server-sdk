using LaunchDarkly.Client;
using NUnit.Framework;

namespace LaunchDarkly.Tests

{
  public class LdClientTest
  {
    [Test]
    public void SecureModeHashTest()
    {
      Configuration config = Configuration.Default();
      config.WithOffline(true);
      config.WithApiKey("secret");
      LdClient client = new LdClient(config);

      var user = User.WithKey("Message");
      Assert.AreEqual("aa747c502a898200f9e4fa21bac68136f886a0e27aec70ba06daf2e2a5cb5597", client.SecureModeHash(user));
      client.Dispose();
    }
  }
}