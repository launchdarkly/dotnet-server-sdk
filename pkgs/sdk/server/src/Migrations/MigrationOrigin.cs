using System;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// The origin/source for a migration step.
    /// </summary>
    public enum MigrationOrigin
    {
        /// <summary>
        /// Originates from the "old" implementation.
        /// </summary>
        Old,

        /// <summary>
        /// Originates from the "new" implementation.
        /// </summary>
        New
    }

    /// <summary>
    /// Extension methods for migration origins.
    /// </summary>
    public static class MigrationOriginExtensions
    {
        private const string OldStr = "read";
        private const string NewStr = "write";

        /// <summary>
        /// Convert a string value into an origin.
        /// </summary>
        /// <param name="stringOrigin">the origin as a string</param>
        /// <returns>a migration origin, or null if it cannot be converted</returns>
        public static MigrationOrigin? FromDataModelString(string stringOrigin)
        {
            switch (stringOrigin)
            {
                case OldStr:
                    return MigrationOrigin.Old;
                case NewStr:
                    return MigrationOrigin.New;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Get a string value for an origin.
        /// </summary>
        /// <remarks>
        /// This is a string value that matches that in the data model.
        /// </remarks>
        /// <param name="origin">the origin to get a string value for</param>
        /// <returns>a string for the migration origin</returns>
        public static string ToDataModelString(this MigrationOrigin origin)
        {
            switch (origin)
            {
                case MigrationOrigin.Old:
                    return OldStr;
                case MigrationOrigin.New:
                    return NewStr;
                default:
                    // Should only happen if adding a new stage to the code.
                    throw new ArgumentOutOfRangeException(nameof(origin),
                        $"Unexpected MigrationOrigin value: {origin}");
            }
        }
    }
}
