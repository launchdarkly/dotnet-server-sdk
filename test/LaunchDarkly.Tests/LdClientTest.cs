using LaunchDarkly.Client;
using Xunit;

namespace LaunchDarkly.Tests

{
  public class LdClientTest
  {
    [Fact]
    public void SecureModeHashTest()
    {
      Configuration config = Configuration.Default();
      config.WithOffline(true);
      config.WithSdkKey("secret");
      LdClient client = new LdClient(config);

      var user = User.WithKey("Message");
      Assert.Equal("aa747c502a898200f9e4fa21bac68136f886a0e27aec70ba06daf2e2a5cb5597", client.SecureModeHash(user));
      client.Dispose();
    }
  }
}