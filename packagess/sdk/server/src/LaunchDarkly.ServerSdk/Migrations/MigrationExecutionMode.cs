namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// Execution mode for a migration.
    /// </summary>
    /// <remarks>
    /// This applies only to a single read operation, not multiple reads using the same migration.
    /// </remarks>
    public enum MigrationExecutionMode
    {
        /// <summary>
        /// Execute one read fully before executing another read.
        /// </summary>
        Serial,

        /// <summary>
        /// Start reads in parallel and wait for them to both finish.
        /// </summary>
        Parallel,
    }
}
