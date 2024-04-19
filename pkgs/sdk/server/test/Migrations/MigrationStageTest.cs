using Xunit;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    public class MigrationStageTest
    {
        [Theory]
        [InlineData(MigrationStage.Off, "off")]
        [InlineData(MigrationStage.DualWrite, "dualwrite")]
        [InlineData(MigrationStage.Shadow, "shadow")]
        [InlineData(MigrationStage.Live, "live")]
        [InlineData(MigrationStage.RampDown, "rampdown")]
        [InlineData(MigrationStage.Complete, "complete")]
        public void ItCanConvertToAString(MigrationStage stage, string expected)
        {
            Assert.Equal(expected, stage.ToDataModelString());
        }

        [Theory]
        [InlineData(MigrationStage.Off, "off")]
        [InlineData(MigrationStage.DualWrite, "dualwrite")]
        [InlineData(MigrationStage.Shadow, "shadow")]
        [InlineData(MigrationStage.Live, "live")]
        [InlineData(MigrationStage.RampDown, "rampdown")]
        [InlineData(MigrationStage.Complete, "complete")]
        public void ItCanConvertFromAString(MigrationStage expected, string stringValue)
        {
            Assert.Equal(expected, MigrationStageExtensions.FromDataModelString(stringValue));
        }
    }
}
