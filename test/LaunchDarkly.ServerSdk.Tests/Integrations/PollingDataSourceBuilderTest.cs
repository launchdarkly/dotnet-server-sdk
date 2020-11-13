using System;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class PollingDataSourceBuilderTest
    {
        private readonly BuilderInternalTestUtil<PollingDataSourceBuilder> _tester =
            BuilderTestUtil.For(Components.PollingDataSource);

        [Fact]
        public void BaseUri()
        {
            var prop = _tester.Property(b => b._baseUri, (b, v) => b.BaseUri(v));
            prop.AssertDefault(PollingDataSourceBuilder.DefaultBaseUri);
            prop.AssertCanSet(new Uri("http://x"));
            prop.AssertSetIsChangedTo(null, PollingDataSourceBuilder.DefaultBaseUri);
        }

        [Fact]
        public void PollInterval()
        {
            var prop = _tester.Property(b => b._pollInterval, (b, v) => b.PollInterval(v));
            prop.AssertDefault(PollingDataSourceBuilder.DefaultPollInterval);
            prop.AssertCanSet(TimeSpan.FromMinutes(7));
            prop.AssertSetIsChangedTo(
                PollingDataSourceBuilder.DefaultPollInterval.Subtract(TimeSpan.FromMilliseconds(1)),
                PollingDataSourceBuilder.DefaultPollInterval);
        }
    }
}
