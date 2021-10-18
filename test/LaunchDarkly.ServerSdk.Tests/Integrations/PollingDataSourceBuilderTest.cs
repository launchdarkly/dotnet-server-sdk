using System;
using LaunchDarkly.TestHelpers;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class PollingDataSourceBuilderTest
    {
        private readonly BuilderBehavior.InternalStateTester<PollingDataSourceBuilder> _tester =
            BuilderBehavior.For(Components.PollingDataSource);

        [Fact]
        public void BaseUri()
        {
#pragma warning disable CS0618  // using deprecated symbol
            var prop = _tester.Property(b => b._baseUri, (b, v) => b.BaseUri(v));
#pragma warning restore CS0618
            prop.AssertDefault(null);
            prop.AssertCanSet(new Uri("http://x"));
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
