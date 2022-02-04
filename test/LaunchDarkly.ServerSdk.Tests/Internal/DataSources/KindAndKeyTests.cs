using Xunit;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class KindAndKeyTests : BaseTest
    {
        [Fact]
        public void Equatable_WhenEqual()
        {
            KindAndKey x = new KindAndKey(DataModel.Features, "foo");
            KindAndKey y = new KindAndKey(DataModel.Features, "foo");
            Assert.True(x.Equals(y));
        }

        [Fact]
        public void Equatable_WhenKindDifferent()
        {
            KindAndKey x = new KindAndKey(DataModel.Features, "foo");
            KindAndKey y = new KindAndKey(DataModel.Segments, "foo");
            Assert.False(x.Equals(y));
        }

        [Fact]
        public void Equatable_WhenKeyDifferent()
        {
            KindAndKey x = new KindAndKey(DataModel.Features, "foo");
            KindAndKey y = new KindAndKey(DataModel.Features, "bar");
            Assert.False(x.Equals(y));
        }
    }
}