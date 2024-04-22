using System;
using LaunchDarkly.Sdk.Server.Internal;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class ServiceEndpointsBuilderTest
    {
        [Fact]
        public void UsesAllDefaultUrisIfNoneAreOverridden()
        {
            var se = Components.ServiceEndpoints().Build();
            Assert.Equal(StandardEndpoints.DefaultEventsBaseUri, se.EventsBaseUri);
            Assert.Equal(StandardEndpoints.DefaultPollingBaseUri, se.PollingBaseUri);
            Assert.Equal(StandardEndpoints.DefaultStreamingBaseUri, se.StreamingBaseUri);
        }

        [Fact]
        public void CanSetAllUrisToCustomValues()
        {
            var eu = new Uri("http://my-events");
            var pu = new Uri("http://my-polling");
            var su = new Uri("http://my-streaming");
            var se = Components.ServiceEndpoints().Events(eu).Polling(pu).Streaming(su).Build();
            Assert.Equal(eu, se.EventsBaseUri);
            Assert.Equal(pu, se.PollingBaseUri);
            Assert.Equal(su, se.StreamingBaseUri);
        }

        [Fact]
        public void IfCustomUrisAreSetAnyUnsetOnesDefaultToNull()
        {
            // See ServiceEndpointsBuilder.Build() for the rationale here
            var eu = new Uri("http://my-events");
            var pu = new Uri("http://my-polling");
            var su = new Uri("http://my-streaming");

            var se1 = Components.ServiceEndpoints().Events(eu).Build();
            Assert.Equal(eu, se1.EventsBaseUri);
            Assert.Null(se1.PollingBaseUri);
            Assert.Null(se1.StreamingBaseUri);

            var se2 = Components.ServiceEndpoints().Polling(pu).Build();
            Assert.Null(se2.EventsBaseUri);
            Assert.Equal(pu, se2.PollingBaseUri);
            Assert.Null(se2.StreamingBaseUri);

            var se3 = Components.ServiceEndpoints().Streaming(su).Build();
            Assert.Null(se3.EventsBaseUri);
            Assert.Null(se3.PollingBaseUri);
            Assert.Equal(su, se3.StreamingBaseUri);
        }

        [Fact]
        public void SettingRelayProxyUriSetsAllUris()
        {
            var ru = new Uri("http://my-relay");
            var se = Components.ServiceEndpoints().RelayProxy(ru).Build();
            Assert.Equal(ru, se.EventsBaseUri);
            Assert.Equal(ru, se.PollingBaseUri);
            Assert.Equal(ru, se.StreamingBaseUri);
        }

        [Fact]
        public void StringSettersAreEquivalentToUriSetters()
        {
            var eu = "http://my-events";
            var pu = "http://my-polling";
            var su = "http://my-streaming";
            var se1 = Components.ServiceEndpoints().Events(eu).Polling(pu).Streaming(su).Build();
            Assert.Equal(new Uri(eu), se1.EventsBaseUri);
            Assert.Equal(new Uri(pu), se1.PollingBaseUri);
            Assert.Equal(new Uri(su), se1.StreamingBaseUri);

            var ru = "http://my-relay";
            var se2 = Components.ServiceEndpoints().RelayProxy(ru).Build();
            Assert.Equal(new Uri(ru), se2.EventsBaseUri);
            Assert.Equal(new Uri(ru), se2.PollingBaseUri);
            Assert.Equal(new Uri(ru), se2.StreamingBaseUri);
        }
    }
}
