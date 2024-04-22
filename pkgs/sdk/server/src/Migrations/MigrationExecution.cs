namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// This class is used to control the execution mechanism for migrations.
    /// </summary>
    /// <remarks>
    /// <para>
    ///  Read operations may be executed in parallel, sequentially in a fixed order, or sequentially in a randomized order.
    /// </para>
    /// <para>
    /// This class facilitates correct combinations of parallel/serial with random/fixed.
    /// </para>
    /// </remarks>
    public readonly struct MigrationExecution
    {
        /// <summary>
        /// The execution mode to use for a migration.
        /// </summary>
        public MigrationExecutionMode Mode { get; }

        /// <summary>
        /// If the execution mode is serial, then this indicates if it should be in a random or fixed order.
        /// </summary>
        public MigrationSerialOrder Order { get; }

        private MigrationExecution(MigrationExecutionMode mode, MigrationSerialOrder order)
        {
            Mode = mode;
            Order = order;
        }

        /// <summary>
        /// Construct a serial execution with the specified ordering.
        /// </summary>
        /// <param name="order">the serial execution order fixed/random</param>
        /// <returns>a migration execution instance</returns>
        public static MigrationExecution Serial(MigrationSerialOrder order)
        {
            return new MigrationExecution(MigrationExecutionMode.Serial, order);
        }

        /// <summary>
        /// Construct a parallel execution.
        /// </summary>
        /// <returns>a migration execution instance</returns>
        public static MigrationExecution Parallel()
        {
            return new MigrationExecution(MigrationExecutionMode.Parallel, MigrationSerialOrder.Random);
        }

        /// <summary>
        /// Produce a string representation. This is intended for informational purposes only and subject to change.
        /// </summary>
        /// <returns>a string representation</returns>
        public override string ToString()
        {
            return Mode == MigrationExecutionMode.Parallel ? "Parallel" : $"Serial-{Order.ToString()}";
        }
    }
}
