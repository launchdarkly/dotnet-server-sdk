using System.Collections.Immutable;

namespace LaunchDarkly.Sdk.Server.Hooks
{
    /// <summary>
    /// Builder for constructing series data, which is passed to between <see cref="Hook"/> methods.
    ///
    /// Use of this builder is optional; it is provided for convenience.
    ///
    /// <example>
    /// <code>
    /// // ImmutableDictionary passed into Hook method:
    /// var data = ...
    /// // Add a new key and return an updated dictionary:
    /// return new SeriesDataBuilder(data).Set("key", "value").Build();
    /// </code>
    /// </example>
    /// </summary>
    public sealed class SeriesDataBuilder
    {
        private readonly ImmutableDictionary<string, object>.Builder _builder;

        /// <summary>
        /// Constructs a new builder from pre-existing series data.
        /// </summary>
        /// <param name="dictionary">pre-existing series data</param>
        public SeriesDataBuilder(ImmutableDictionary<string, object> dictionary)
        {
            _builder = dictionary.ToBuilder();
        }

        /// <summary>
        /// Constructs a new builder with empty series data.
        /// </summary>
        public SeriesDataBuilder(): this(ImmutableDictionary<string, object>.Empty) {}


        /// <summary>
        /// Sets a key-value pair.
        /// </summary>
        /// <param name="key">key of value</param>
        /// <param name="value">the value to set</param>
        /// <returns>this builder</returns>
        public SeriesDataBuilder Set(string key, object value)
        {
            _builder[key] = value;
            return this;
        }

        /// <summary>
        /// Returns a SeriesData based on the current state of the builder.
        /// </summary>
        /// <returns>new series data</returns>
        public ImmutableDictionary<string, object> Build()
        {
            return _builder.Count == 0 ? ImmutableDictionary<string, object>.Empty : _builder.ToImmutable();
        }
    }
}
