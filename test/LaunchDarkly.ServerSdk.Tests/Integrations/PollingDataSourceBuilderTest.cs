using System;
using Xunit;

using static LaunchDarkly.Sdk.Server.Components;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class PollingDataSourceBuilderTest
    {
        [Fact]
        public void BaseUri()
        {
            Assert.Equal(PollingDataSourceBuilder.DefaultBaseUri,
                PollingDataSource()._baseUri);

            Assert.Equal(new Uri("http://x"),
                PollingDataSource().BaseUri(new Uri("http://x"))._baseUri);

            Assert.Equal(PollingDataSourceBuilder.DefaultBaseUri,
                PollingDataSource().BaseUri(null)._baseUri);
        }

        [Fact]
        public void PollInterval()
        {
            Assert.Equal(PollingDataSourceBuilder.DefaultPollInterval,
                PollingDataSource()._pollInterval);

            Assert.Equal(TimeSpan.FromMinutes(7),
                PollingDataSource().PollInterval(TimeSpan.FromMinutes(7))._pollInterval);

            Assert.Equal(PollingDataSourceBuilder.DefaultPollInterval,
                PollingDataSource().PollInterval(TimeSpan.FromMilliseconds(1))._pollInterval);
        }
    }
}
