using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using static LaunchDarkly.TestHelpers.JsonAssertions;

namespace LaunchDarkly.Sdk.Server
{
    public static class AssertHelpers
    {
        public static void DataSetsEqual(FullDataSet<ItemDescriptor> expected, FullDataSet<ItemDescriptor> actual) =>
            AssertJsonEqual(TestUtils.DataSetAsJson(expected), TestUtils.DataSetAsJson(actual));

        public static void DataItemsEqual(DataKind kind, ItemDescriptor expected, ItemDescriptor actual)
        {
            AssertJsonEqual(kind.Serialize(expected), kind.Serialize(actual));
            Assert.Equal(expected.Version, actual.Version);
        }
    }
}
