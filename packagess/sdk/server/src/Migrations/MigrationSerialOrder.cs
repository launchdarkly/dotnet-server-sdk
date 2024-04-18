namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// When using serial execution controls the order reads are executed.
    /// </summary>
    public enum MigrationSerialOrder
    {
        /// <summary>
        /// Each time a read is performed randomize the order.
        /// </summary>
        Random,

        /// <summary>
        /// Always execute reads in the same order.
        /// </summary>
        Fixed
    }
}
