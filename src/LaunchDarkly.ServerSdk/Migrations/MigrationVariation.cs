namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// The result of an <see cref="MigrationVariation"/> call.
    /// </summary>
    public readonly struct MigrationVariation
    {
        /// <summary>
        /// The result of the flag evaluation. This will be either one of the flag's variations or
        /// the default value that was passed to <see cref="MigrationVariation"/>.
        /// </summary>
        public MigrationStage Stage { get; }

        /// <summary>
        /// A tracker which can be used to generate analytics for the migration.
        /// </summary>
        public MigrationOpTracker Tracker { get; }

        internal MigrationVariation(MigrationStage stage, MigrationOpTracker tracker)
        {
            Stage = stage;
            Tracker = tracker;
        }

        /// <summary>
        /// Deconstruct the MigrationVariation into the stage and tracker.
        /// </summary>
        /// <remarks>
        /// <code>
        /// var (stage, tracker) = client.MigrationVariation(flagKey, context, defaultStage);
        /// </code>
        /// </remarks>
        /// <param name="stage">the stage of the variation</param>
        /// <param name="tracker">the tracker for the variation</param>
        public void Deconstruct(out MigrationStage stage, out MigrationOpTracker tracker)
        {
            stage = Stage;
            tracker = Tracker;
        }
    }
}
