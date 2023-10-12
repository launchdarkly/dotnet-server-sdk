namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// The result of a migration write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A migration write result will always include an authoritative result, and it may contain a non-authoritative
    /// result.
    /// </para>
    /// <para>
    /// Not all migration stages will execute both writes, and in the case of a write error from the authoritative
    /// source then the non-authoritative write will not be executed.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">the type of the result</typeparam>
    public class MigrationWriteResult<TResult> where TResult: class
    {
        /// <summary>
        /// The authoritative result of the write.
        /// </summary>
        public MigrationResult<TResult> Authoritative { get; }

        /// <summary>
        /// The non-authoritative result of the write.
        /// </summary>
        public MigrationResult<TResult>? NonAuthoritative { get; }

        internal MigrationWriteResult(
            MigrationResult<TResult> authoritative,
            MigrationResult<TResult>? nonAuthoritative = null)
        {
            Authoritative = authoritative;
            NonAuthoritative = nonAuthoritative;
        }
    }
}
