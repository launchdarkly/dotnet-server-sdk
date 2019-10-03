using System;
using LaunchDarkly.Client;
using LaunchDarkly.Client.Files;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class D2LUseCaseTest
    {
        [Fact]
        public void Test()
        {
            Configuration config = Configuration
                 .Builder(sdkKey: null)
                 .EventProcessorFactory(Components.NullEventProcessor)
                 .UpdateProcessorFactory(
                     FileComponents
                     .FileDataSource()
                     .WithFilePaths(@"C:\D2L\ld-features.json")
                 )
                 .Build();

            LdClient client = new LdClient(config);
            User user = User.WithKey("test");

            bool value = client.BoolVariation("broadcast-aws-iot-https-publish", user, defaultValue: false);
            Console.WriteLine("Value: {0}", value);
        }
    }
}
