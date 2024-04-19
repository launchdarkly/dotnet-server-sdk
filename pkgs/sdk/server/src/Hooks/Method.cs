namespace LaunchDarkly.Sdk.Server.Hooks
{
    /// <summary>
    /// Method represents the SDK client method that triggered a hook invocation. Fixing some commenting. Again.
    /// </summary>
   #pragma warning disable 1591
    public static class Method
    {
        public const string BoolVariation = "LdClient.BoolVariation";
        public const string BoolVariationDetail = "LdClient.BoolVariationDetail";
        public const string IntVariation = "LdClient.IntVariation";
        public const string IntVariationDetail = "LdClient.IntVariationDetail";
        public const string FloatVariation = "LdClient.FloatVariation";
        public const string FloatVariationDetail = "LdClient.FloatVariationDetail";
        public const string DoubleVariation = "LdClient.DoubleVariation";
        public const string DoubleVariationDetail = "LdClient.DoubleVariationDetail";
        public const string StringVariation = "LdClient.StringVariation";
        public const string StringVariationDetail = "LdClient.StringVariationDetail";
        public const string JsonVariation = "LdClient.JsonVariation";
        public const string JsonVariationDetail = "LdClient.JsonVariationDetail";
        public const string MigrationVariation = "LdClient.MigrationVariation";
    }
    #pragma warning restore 1591
}
