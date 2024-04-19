using System;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// This class represents the result of a migration operation.
    /// </summary>
    /// <remarks>
    /// In the case of a read operation the result will be this type. Write operations may need to return multiple
    /// results and therefore use the <see cref="MigrationWriteResult{TResult}"/> type.
    /// </remarks>
    /// <typeparam name="TResult">the type of the result</typeparam>
    public struct MigrationResult<TResult> where TResult : class
    {
        /// <summary>
        /// The value of a successful result.
        /// </summary>
        /// <remarks>
        /// A failed result will not set the value.
        /// </remarks>
        public TResult Value { get; }

        /// <summary>
        /// True if the result was a success.
        /// </summary>
        public bool IsSuccessful { get; }

        /// <summary>
        /// Any exception which happened for the operation. Will not be set if the result was successful.
        /// </summary>
        /// <para>
        /// If a migration method fails, but does not throw an exception, then this will also be null.
        /// </para>
        public Exception Exception { get; }

        /// <summary>
        /// The origin of the result.
        /// </summary>
        public MigrationOrigin Origin { get; }

        internal MigrationResult(MigrationOrigin origin, bool isSuccessful, TResult result, Exception exception)
        {
            Value = result;
            IsSuccessful = isSuccessful;
            Exception = exception;
            Origin = origin;
        }
    }
}
