using System;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// This builder is used to construct <see cref="Migration{TReadResult,TWriteResult,TReadInput,TWriteInput}"/> instances.
    /// </summary>
    /// <remarks>
    /// This class is not thread-safe. The builder should be used on one thread and then the
    /// built <see cref="Migration{TReadResult,TWriteResult,TReadInput,TWriteInput}"/> is thread safe.
    /// </remarks>
    /// <typeparam name="TReadResult">the result type for reads</typeparam>
    /// <typeparam name="TWriteResult">the result type for writes</typeparam>
    /// <typeparam name="TReadInput">the input types for reads</typeparam>
    /// <typeparam name="TWriteInput">the input type for writes</typeparam>
    public sealed class MigrationBuilder<TReadResult, TWriteResult, TReadInput, TWriteInput>
        where TReadResult : class
        where TWriteResult : class
    {
        private readonly ILdClient _client;
        private bool _trackLatency = true;
        private bool _trackErrors = true;
        private MigrationExecution _execution = MigrationExecution.Parallel();

        private Func<TReadInput, MigrationMethod.Result<TReadResult>> _readOld;
        private Func<TReadInput, MigrationMethod.Result<TReadResult>> _readNew;

        private Func<TWriteInput, MigrationMethod.Result<TWriteResult>> _writeOld;
        private Func<TWriteInput, MigrationMethod.Result<TWriteResult>> _writeNew;

        private Func<TReadResult, TReadResult, bool> _check;

        /// <summary>
        /// Construct a migration builder.
        /// </summary>
        /// <param name="client">the client to use for migrations</param>
        public MigrationBuilder(ILdClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Enable or disable latency tracking. Tracking is enabled by default.
        /// </summary>
        /// <param name="trackLatency">true to enable tracking, false to disable it</param>
        /// <returns>a reference to this builder</returns>
        public MigrationBuilder<TReadResult, TWriteResult, TReadInput, TWriteInput> TrackLatency(bool trackLatency)
        {
            _trackLatency = trackLatency;
            return this;
        }

        /// <summary>
        /// Enable or disable error tracking. Tracking is enabled by default.
        /// </summary>
        /// <param name="trackErrors">true to enable error tracking, false to disable it</param>
        /// <returns>a reference to this builder</returns>
        public MigrationBuilder<TReadResult, TWriteResult, TReadInput, TWriteInput> TrackErrors(bool trackErrors)
        {
            _trackErrors = trackErrors;
            return this;
        }

        /// <summary>
        /// Influences the level of concurrency when the migration stage calls for multiple execution reads.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default read execution is <see cref="MigrationExecution.Parallel"/>.
        /// </para>
        /// <para>
        /// Setting the execution to randomized serial order.
        /// </para>
        /// <code>
        /// var builder = new MigrationBuilder&lt;string, string, string, string&gt;()
        ///     .ReadExecution(MigrationExecution.Serial(MigrationSerialOrder.Random))
        /// </code>
        /// </remarks>
        /// <param name="execution">the execution configuration</param>
        /// <returns>a reference to this builder</returns>
        public MigrationBuilder<TReadResult, TWriteResult, TReadInput, TWriteInput> ReadExecution(
            MigrationExecution execution)
        {
            _execution = execution;
            return this;
        }

        /// <summary>
        /// Configure the read methods of the migration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Users are required to provide two different read methods -- one to read from the old migration source, and one to
        /// read from the new source. This method allows specifying a check method for consistency tracking.
        /// </para>
        /// <para>
        /// If you do not want consistency tracking, then use
        /// <see cref="MigrationBuilder{TReadResult,TWriteResult,TReadInput,TWriteInput}.Read(Func{TReadInput, MigrationMethod.Result{TReadResult}}, Func{TReadInput, MigrationMethod.Result{TReadResult}})"/>.
        /// </para>
        /// </remarks>
        /// <param name="readOld">method for reading from the "old" migration source</param>
        /// <param name="readNew">method for reading from the "new" migration source</param>
        /// <param name="check">method which checks the consistency of the "old" and "new" source</param>
        /// <returns>a reference to this builder</returns>
        public MigrationBuilder<TReadResult, TWriteResult, TReadInput, TWriteInput> Read(
            Func<TReadInput, MigrationMethod.Result<TReadResult>> readOld,
            Func<TReadInput, MigrationMethod.Result<TReadResult>> readNew,
            Func<TReadResult, TReadResult, bool> check)
        {
            _readOld = readOld;
            _readNew = readNew;
            _check = check;
            return this;
        }

        /// <summary>
        /// Configure the read methods of the migration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Users are required to provide two different read methods -- one to read from the old migration source, and one to
        /// read from the new source. This method does not enable consistency tracking.
        /// </para>
        /// <para>
        /// If you do want consistency tracking, then use
        /// <see cref="MigrationBuilder{TReadResult,TWriteResult,TReadInput,TWriteInput}.Read(Func{TReadInput, MigrationMethod.Result{TReadResult}}, Func{TReadInput, MigrationMethod.Result{TReadResult}}, Func{TReadResult, TReadResult, bool})"/>.
        /// </para>
        /// </remarks>
        /// <param name="readOld">method for reading from the "old" migration source</param>
        /// <param name="readNew">method for reading from the "new" migration source</param>
        /// <returns>a reference to this builder</returns>
        public MigrationBuilder<TReadResult, TWriteResult, TReadInput, TWriteInput> Read(
            Func<TReadInput, MigrationMethod.Result<TReadResult>> readOld,
            Func<TReadInput, MigrationMethod.Result<TReadResult>> readNew)
        {
            Read(readOld, readNew, null);
            return this;
        }

        /// <summary>
        /// Configure the write methods of the migration.
        /// </summary>
        /// <remarks>
        /// Users are required to provide two different write methods -- one to write to the old migration source, and one to
        /// write to the new source. Not every stage requires
        /// </remarks>
        /// <param name="writeOld">method which writes to the "old" source</param>
        /// <param name="writeNew">method which writes to the "new" source</param>
        /// <returns>a reference to this builder</returns>
        public MigrationBuilder<TReadResult, TWriteResult, TReadInput, TWriteInput> Write(
            Func<TWriteInput, MigrationMethod.Result<TWriteResult>> writeOld,
            Func<TWriteInput, MigrationMethod.Result<TWriteResult>> writeNew)
        {
            _writeOld = writeOld;
            _writeNew = writeNew;
            return this;
        }

        /// <summary>
        /// Build a <see cref="Migration{TReadResult,TWriteResult,TReadInput,TWriteInput}"/>.
        /// </summary>
        /// <remarks>
        /// A migration requires that both the read and write methods are defined. If they have not been defined, then
        /// a migration cannot be constructed. In this case an empty optional will be returned.
        /// </remarks>
        /// <returns>a build migration or null if a migration could not be built</returns>
        public IMigration<TReadResult, TWriteResult, TReadInput, TWriteInput> Build()
        {
            if (_readOld == null ||
                _readNew == null ||
                _writeOld == null ||
                _writeNew == null)
            {
                return null;
            }

            return new Migration<TReadResult, TWriteResult, TReadInput, TWriteInput>(
                _client,
                _trackLatency,
                _trackErrors,
                _execution,
                _readOld,
                _readNew,
                _writeOld,
                _writeNew,
                _check);
        }
    }
}
