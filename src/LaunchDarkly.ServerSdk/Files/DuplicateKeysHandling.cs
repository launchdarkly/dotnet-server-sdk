namespace LaunchDarkly.Client.Files
{
    /// <summary>
    /// Determines how duplicate versioned data keys are handled.
    /// </summary>
    public enum DuplicateKeysHandling
    {
        /// <summary>
        /// An exception will be thrown if keys are duplicated across files.
        /// </summary>
        Throw,

        /// <summary>
        /// Keys that are duplicated across files will be ignored, and the first occurrence will be used.
        /// </summary>
        Ignore
    }
}
