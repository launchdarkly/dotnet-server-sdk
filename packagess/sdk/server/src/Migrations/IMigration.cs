namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// Interface for managing technology migrations
    /// </summary>
    /// <typeparam name="TReadResult">the result type for reads</typeparam>
    /// <typeparam name="TWriteResult">the result type for writes</typeparam>
    /// <typeparam name="TReadInput">the input types for reads</typeparam>
    /// <typeparam name="TWriteInput">the input type for writes</typeparam>
    public interface IMigration<TReadResult, TWriteResult, in TReadInput, in TWriteInput>
        where TReadResult : class
        where TWriteResult : class
    {
        /// <summary>
        /// Execute a migration based read with a payload.
        /// </summary>
        /// <remarks>
        /// To execute a read without a payload use
        /// <see cref="IMigration{TReadResult,TWriteResult,TReadInput,TWriteInput}.Read(string, Context, MigrationStage)"/>.
        /// </remarks>
        /// <param name="flagKey">the flag key of migration flag</param>
        /// <param name="context">the context for the migration</param>
        /// <param name="defaultStage">the default migration stage</param>
        /// <param name="payload">payload that will be passed to the new/old read implementations</param>
        /// <returns>the result of the read</returns>
        MigrationResult<TReadResult> Read(
            string flagKey,
            Context context,
            MigrationStage defaultStage,
            TReadInput payload);

        /// <summary>
        /// Execute a migration based write with a payload.
        /// </summary>
        /// <remarks>
        /// To execute a write without a payload use
        /// <see cref="IMigration{TReadResult,TWriteResult,TReadInput,TWriteInput}.Write(string, Context, MigrationStage)"/>.
        /// </remarks>
        /// <param name="flagKey">the flag key of migration flag</param>
        /// <param name="context">the context for the migration</param>
        /// <param name="defaultStage">the default migration stage</param>
        /// <param name="payload">payload that will be passed to the new/old write implementations</param>
        /// <returns>the result of the write operation</returns>
        MigrationWriteResult<TWriteResult> Write(
            string flagKey,
            Context context,
            MigrationStage defaultStage,
            TWriteInput payload);

        /// <summary>
        /// Execute a migration based read.
        /// </summary>
        /// <remarks>
        /// <para>
        /// To execute a read with a payload use
        /// <see cref="IMigration{TReadResult,TWriteResult,TReadInput,TWriteInput}.Read(string, Context, MigrationStage, TReadInput)"/>.
        /// </para>
        /// <para>
        /// When no payload is provided a <see langword="default" /> value will be used for the payload.
        /// </para>
        /// </remarks>
        /// <param name="flagKey">the flag key of migration flag</param>
        /// <param name="context">the context for the migration</param>
        /// <param name="defaultStage">the default migration stage</param>
        /// <returns>the result of the read</returns>
        MigrationResult<TReadResult> Read(
            string flagKey,
            Context context,
            MigrationStage defaultStage);

        /// <summary>
        /// Execute a migration based write.
        /// </summary>
        /// <remarks>
        /// <para>
        /// To execute a write with a payload use
        /// <see cref="IMigration{TReadResult,TWriteResult,TReadInput,TWriteInput}.Write(string, Context, MigrationStage, TWriteInput)"/>.
        /// </para>
        /// <para>
        /// When no payload is provided a <see langword="default" /> value will be used for the payload.
        /// </para>
        /// </remarks>
        /// <param name="flagKey">the flag key of migration flag</param>
        /// <param name="context">the context for the migration</param>
        /// <param name="defaultStage">the default migration stage</param>
        /// <returns>the result of the write operation</returns>
        MigrationWriteResult<TWriteResult> Write(
            string flagKey,
            Context context,
            MigrationStage defaultStage);
    }
}
