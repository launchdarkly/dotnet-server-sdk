using System;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// The type of migration operation.
    /// </summary>
    public enum MigrationOperation
    {
        /// <summary>
        /// A read operation.
        /// </summary>
        Read,

        /// <summary>
        /// A write operation.
        /// </summary>
        Write
    }

    /// <summary>
    /// Extension methods for migration operations.
    /// </summary>
    public static class MigrationOperationExtensions
    {
        private const string ReadStr = "read";
        private const string WriteStr = "write";

        /// <summary>
        /// Convert a string value into an operation.
        /// </summary>
        /// <param name="stringOperation">the operation as a string</param>
        /// <returns>a migration operation, or null if it cannot be converted</returns>
        public static MigrationOperation? FromDataModelString(string stringOperation)
        {
            switch (stringOperation)
            {
                case ReadStr:
                    return MigrationOperation.Read;
                case WriteStr:
                    return MigrationOperation.Write;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Get a string value for an operation.
        /// </summary>
        /// <remarks>
        /// This is a string value that matches that in the data model.
        /// </remarks>
        /// <param name="operation">the operation to get a string value for</param>
        /// <returns>a string for the migration operation</returns>
        public static string ToDataModelString(this MigrationOperation operation)
        {
            switch (operation)
            {
                case MigrationOperation.Read:
                    return ReadStr;
                case MigrationOperation.Write:
                    return WriteStr;
                default:
                    // Should only happen if adding a new stage to the code.
                    throw new ArgumentOutOfRangeException(nameof(operation),
                        $"Unexpected MigrationOperation value: {operation}");
            }
        }
    }
}
