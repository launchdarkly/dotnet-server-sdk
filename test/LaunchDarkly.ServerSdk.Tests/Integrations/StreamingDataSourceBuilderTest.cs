using System;
using LaunchDarkly.TestHelpers;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class StreamingDataSourceBuilderTest
    {
        private readonly BuilderBehavior.InternalStateTester<StreamingDataSourceBuilder> _tester =
            BuilderBehavior.For(Components.StreamingDataSource);

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
        public void InitialReconnectDelay()
        {
            var prop = _tester.Property(b => b._initialReconnectDelay, (b, v) => b.InitialReconnectDelay(v));
            prop.AssertDefault(StreamingDataSourceBuilder.DefaultInitialReconnectDelay);
            prop.AssertCanSet(TimeSpan.FromMilliseconds(222));
        }
    }
}
