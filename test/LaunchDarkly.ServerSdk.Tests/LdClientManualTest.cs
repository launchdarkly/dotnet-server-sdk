using System;
using Xunit;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientManualTest
    {
        private static string API_KEY = "YOUR_SDK_KEY";
        private static string FLAG_KEY = "YOUR_FLAG_KEY";

//        [Fact]
        public void ClientTest()
        {
            Configuration config = Configuration.Default(API_KEY);
            LdClient client = new LdClient(config);
            Assert.True(client.Initialized());

            var user = User.WithKey("user@test.com");
            bool actual = client.BoolVariation(FLAG_KEY, user, false);

            Assert.True(actual);

            System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            client.Flush();
            client.Dispose();
        }
    }
}