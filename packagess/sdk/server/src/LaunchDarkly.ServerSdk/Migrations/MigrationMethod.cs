using System;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// Static class for creation <see cref="MigrationMethod.Result{TResult}"/> instances.
    /// </summary>
    public static class MigrationMethod
    {
        /// <summary>
        /// Results of a method associated with a migration origin.
        /// </summary>
        /// <typeparam name="TResult">the type of the result</typeparam>
        public readonly struct Result<TResult> where TResult : class
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

            internal Result(bool isSuccessful, TResult result, Exception exception)
            {
                Value = result;
                IsSuccessful = isSuccessful;
                Exception = exception;
            }

            internal MigrationResult<TResult> MigrationResult(MigrationOrigin origin)
            {
                return new MigrationResult<TResult>(
                    origin,
                    IsSuccessful,
                    Value,
                    Exception);
            }
        }

        /// <summary>
        /// Construct a successful result.
        /// </summary>
        /// <param name="result">the value of the result</param>
        /// <typeparam name="TResult">the type of the result</typeparam>
        /// <returns>the successful result</returns>
        public static Result<TResult> Success<TResult>(TResult result) where TResult : class
        {
            return new Result<TResult>(true, result, null);
        }

        /// <summary>
        /// Construct a method result representing a failure.
        /// </summary>
        /// <remarks>
        /// This method doesn't provide any information about the cause of the failure. It is recommended
        /// to throw an exception or use <see cref="MigrationMethod.Failure{TResult}(Exception)"/>.
        /// </remarks>
        /// <typeparam name="TResult">the type of the result</typeparam>
        /// <returns>the failed result</returns>
        public static Result<TResult> Failure<TResult>() where TResult : class
        {
            return new Result<TResult>(false, null, null);
        }

        /// <summary>
        /// Construct a method result representing a failure based on an Exception.
        /// </summary>
        /// <param name="exception">the exception which occurred during method execution</param>
        /// <typeparam name="TResult">the type of the result</typeparam>
        /// <returns>the failed result</returns>
        public static Result<TResult> Failure<TResult>(Exception exception) where TResult : class
        {
            return new Result<TResult>(false, null, exception);
        }
    }
}
