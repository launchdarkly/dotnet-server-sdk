
namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Integration between the LaunchDarkly SDK and file data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file data source allows you to use local files as a source of feature flag state. This would
    /// typically be used in a test environment, to operate using a predetermined feature flag state
    /// without an actual LaunchDarkly connection. See <see cref="DataSource"/> for details.
    /// </para>
    /// <para>
    /// This is different from {@link TestData}, which allows you to simulate flag configurations
    /// programmatically rather than using a file.
    /// </para>
    /// </remarks>
    /// <seealso cref="TestData"/>
    public static class FileData
    {
        /// <summary>
        /// Creates a builder for configuring the file data source.
        /// </summary>
        /// <remarks>
        /// <para>
        /// To use this component, call <see cref="DataSource()"/> to obtain a factory object, call one or
        /// methods to configure it, and then add it to your LaunchDarkly client configuration. At a
        /// minimum, you will want to call <see cref="FileDataSourceBuilder.FilePaths(string[])"/> to specify
        /// your data file(s); you can also use <see cref="FileDataSourceBuilder.AutoUpdate(bool)"/> to
        /// specify that flags should be reloaded when a file is modified. See <see cref="FileDataSourceBuilder"/>
        /// for all configuration options.
        /// </para>
        /// <code>
        ///     var fileSource = FileData.DataSource()
        ///         .FilePaths("./testData/flags.json")
        ///         .AutoUpdate(true);
        ///     var config = Configuration.Builder("sdkKey")
        ///         .DataSource(fileSource)
        ///         .Build();
        /// </code>
        /// <para>
        /// This will cause the client <i>not</i> to connect to LaunchDarkly to get feature flags. The
        /// client may still make network connections to send analytics events, unless you have disabled
        /// this with <c>configuration.EventProcessor(Components.NullEventProcessor)</c>.
        /// </para>
        /// <para>
        /// Flag data files are JSON by default (although it is possible to specify a parser for another format,
        /// such as YAML; see <see cref="FileDataSourceBuilder.Parser(System.Func{string, object})"/>). They
        /// contain an object with three possible properties:
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>flags</c>: Feature flag definitions.</description></item>
        /// <item><description><c>flagVersions</c>: Simplified feature flags that contain only a value.</description></item>
        /// <item><description><c>segments</c>: User segment definitions.</description></item>
        /// </list>
        /// <para>
        /// The format of the data in <c>flags</c> and <c>segments</c> is defined by the LaunchDarkly application
        /// and is subject to change. Rather than trying to construct these objects yourself, it is simpler
        /// to request existing flags directly from the LaunchDarkly server in JSON format, and use this
        /// output as the starting point for your file. In Linux you would do this:
        /// </para>
        /// <code>
        ///     curl -H "Authorization: {your sdk key}" https://sdk.launchdarkly.com/sdk/latest-all
        /// </code>
        /// <para>
        /// The output will look something like this (but with many more properties):
        /// </para>
        /// <code>
        /// {
        ///     "flags": {
        ///         "flag-key-1": {
        ///             "key": "flag-key-1",
        ///             "on": true,
        ///             "variations": [ "a", "b" ]
        ///         }
        ///     },
        ///     "segments": {
        ///         "segment-key-1": {
        ///             "key": "segment-key-1",
        ///             "includes": [ "user-key-1" ]
        ///         }
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Data in this format allows the SDK to exactly duplicate all the kinds of flag behavior supported
        /// by LaunchDarkly. However, in many cases you will not need this complexity, but will just want to
        /// set specific flag keys to specific values. For that, you can use a much simpler format:
        /// </para>
        /// <code>
        /// {
        ///     "flagValues": {
        ///         "my-string-flag-key": "value-1",
        ///         "my-boolean-flag-key": true,
        ///         "my-integer-flag-key": 3
        ///     }
        /// }
        /// </code>
        /// <para>
        /// It is also possible to specify both <c>flags</c> and <c>flagValues</c>, if you want some flags
        /// to have simple values and others to have complex behavior. However, it is an error to use the
        /// same flag key or segment key more than once, either in a single file or across multiple files.
        /// </para>
        /// <para>
        /// If the data source encounters any error in any file-- malformed content, a missing file, or a
        /// duplicate key-- it will not load flags from any of the files.
        /// </para>
        /// </remarks>
        /// <returns>a <see cref="FileDataSourceBuilder"/></returns>
        public static FileDataSourceBuilder DataSource() => new FileDataSourceBuilder();
    }
}
