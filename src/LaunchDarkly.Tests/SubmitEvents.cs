using NUnit.Framework;
using Newtonsoft.Json;
using LaunchDarkly.Client;
using System;

namespace LaunchDarkly.Tests
{
    class SubmitEvents
    {
        [Ignore]
        public void SubmitACustomEvent()
        {
            var config = Configuration.Default()
                                      .WithEventQueueCapacity(1)
                                      .WithEventQueueFrequency(TimeSpan.FromSeconds(1));

            var client = new LdClient(config);
            var user = User.WithKey("user@test.com");

            var customData = new AnyCustomData(1, "A String Value", true);

            var data = JsonConvert.SerializeObject(customData);

            client.SendEvent("CustomEvent001", user, "data");
//            Thread.Sleep(1500);
            client.SendEvent("CustomEvent002", user, "data");
//            Thread.Sleep(1500);
            client.SendEvent("CustomEvent003", user, "data");
//            Thread.Sleep(1500);
        }


    }
}
