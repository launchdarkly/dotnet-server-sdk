using System;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class StreamingDataSourceBuilderTest
    {
        private readonly BuilderInternalTestUtil<StreamingDataSourceBuilder> _tester =
            BuilderTestUtil.For(Components.StreamingDataSource);

        [Fact]
        public void BaseUri()
        {
            var prop = _tester.Property(b => b._baseUri, (b, v) => b.BaseUri(v));
            prop.AssertDefault(StreamingDataSourceBuilder.DefaultBaseUri);
            prop.AssertCanSet(new Uri("http://x"));
            prop.AssertSetIsChangedTo(null, StreamingDataSourceBuilder.DefaultBaseUri);
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
