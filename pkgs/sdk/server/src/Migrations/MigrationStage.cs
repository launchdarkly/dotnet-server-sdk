using System;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// Stage denotes one of six possible stages a technology migration could be a
    /// part of, progressing through the following order.
    /// <para>
    /// Off DualWrite Shadow Live RampDown Complete
    /// </para>
    /// </summary>
    public enum MigrationStage
    {
        /// <summary>
        /// Off - migration hasn't started, "old" is authoritative for reads and writes.
        /// </summary>
        Off,

        /// <summary>
        /// DualWrite - write to both "old" and "new", "old" is authoritative for reads.
        /// </summary>
        DualWrite,

        /// <summary>
        /// Shadow - both "new" and "old" versions run with a preference for "old".
        /// </summary>
        Shadow,

        /// <summary>
        /// Live - both "new" and "old" versions run with a preference for "new".
        /// </summary>
        Live,

        /// <summary>
        /// RampDown - only read from "new", write to "old" and "new".
        /// </summary>
        RampDown,

        /// <summary>
        /// Complete - migration is done.
        /// </summary>
        Complete
    }

    /// <summary>
    /// Extension methods for migration stages.
    /// </summary>
    public static class MigrationStageExtensions
    {
        private const string OffStr = "off";
        private const string DualWriteStr = "dualwrite";
        private const string ShadowStr = "shadow";
        private const string LiveStr = "live";
        private const string RampDownStr = "rampdown";
        private const string CompleteStr = "complete";

        /// <summary>
        /// Convert a string value into a stage.
        /// </summary>
        /// <param name="stringStage"></param>
        /// <returns>a migration stage, or null if it cannot be converted</returns>
        public static MigrationStage? FromDataModelString(string stringStage)
        {
            switch (stringStage)
            {
                case OffStr:
                    return MigrationStage.Off;
                case DualWriteStr:
                    return MigrationStage.DualWrite;
                case ShadowStr:
                    return MigrationStage.Shadow;
                case LiveStr:
                    return MigrationStage.Live;
                case RampDownStr:
                    return MigrationStage.RampDown;
                case CompleteStr:
                    return MigrationStage.Complete;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Get a string value for a stage.
        /// </summary>
        /// <remarks>
        /// This is a string value that matches that in the data model.
        /// </remarks>
        /// <param name="stage">the stage to get a string value for</param>
        /// <returns>a string for the migration stage</returns>
        public static string ToDataModelString(this MigrationStage stage)
        {
            switch (stage)
            {
                case MigrationStage.Off:
                    return OffStr;
                case MigrationStage.DualWrite:
                    return DualWriteStr;
                case MigrationStage.Shadow:
                    return ShadowStr;
                case MigrationStage.Live:
                    return LiveStr;
                case MigrationStage.RampDown:
                    return RampDownStr;
                case MigrationStage.Complete:
                    return CompleteStr;
                default:
                    // Should only happen if adding a new stage to the code.
                    throw new ArgumentOutOfRangeException(nameof(stage),
                        $"Unexpected MigrationStage value: {stage}");
            }
        }
    }
}
