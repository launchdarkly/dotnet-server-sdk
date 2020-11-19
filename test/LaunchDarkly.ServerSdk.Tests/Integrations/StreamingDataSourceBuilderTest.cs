using System;
using Xunit;

using static LaunchDarkly.Sdk.Server.Components;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class StreamingDataSourceBuilderTest
    {
        [Fact]
        public void BaseUri()
        {
            Assert.Equal(StreamingDataSourceBuilder.DefaultBaseUri,
                StreamingDataSource()._baseUri);

            Assert.Equal(new Uri("http://x"),
                StreamingDataSource().BaseUri(new Uri("http://x"))._baseUri);

            Assert.Equal(StreamingDataSourceBuilder.DefaultBaseUri,
                StreamingDataSource().BaseUri(null)._baseUri);
        }

        [Fact]
        public void InitialReconnectDelay()
        {
            Assert.Equal(StreamingDataSourceBuilder.DefaultInitialReconnectDelay,
                StreamingDataSource()._initialReconnectDelay);

            Assert.Equal(TimeSpan.FromMilliseconds(222),
                StreamingDataSource().InitialReconnectDelay(TimeSpan.FromMilliseconds(222))
                ._initialReconnectDelay);
        }
    }
}
