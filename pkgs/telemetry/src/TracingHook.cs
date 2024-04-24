using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using LaunchDarkly.Sdk.Server.Hooks;

namespace LaunchDarkly.Sdk.Server.Telemetry
{

    using SeriesData = ImmutableDictionary<string, object>;

    /// <summary>
    /// TracingHookBuilder creates a <see cref="TracingHook"/>. The hook can be passed into the SDK's Hook configuration
    /// builder <see cref="ConfigurationBuilder.Hooks"/>. To create a TracingHook from a builder, call <see cref="Build"/>.
    /// </summary>
    public class TracingHookBuilder
    {
        private bool _createActivities;
        private bool _includeVariant;

        internal TracingHookBuilder()
        {
            _createActivities = false;
            _includeVariant = false;
        }

        /// <summary>
        /// The TracingHook will create <see cref="Activity"/>s for flag evaluations.
        /// The activities will be children of the current activity, if one exists, or root activities.
        /// Disabled by default.
        ///
        /// NOTE: This is an experimental option; it may be removed and behavior is
        /// subject to change within minor versions.
        /// </summary>
        /// <param name="createActivities">true to create activities, false otherwise</param>
        /// <returns>this builder</returns>
        public TracingHookBuilder CreateActivities(bool createActivities = true)
        {
            _createActivities = createActivities;
            return this;
        }

        /// <summary>
        /// The TracingHook will include the flag variant in the current activity, if one exists.
        /// The variant representation is a JSON string. Disabled by default.
        /// </summary>
        /// <param name="includeVariant">true to include variants, false otherwise</param>
        /// <returns>this builder</returns>
        public TracingHookBuilder IncludeVariant(bool includeVariant = true)
        {
            _includeVariant = includeVariant;
            return this;
        }

        /// <summary>
        /// Builds the <see cref="TracingHook"/> with the configured options.
        ///
        /// The hook may be passed into <see cref="ConfigurationBuilder.Hooks"/>.
        /// </summary>
        /// <returns>the new hook</returns>
        public TracingHook Build()
        {
            return new TracingHook(new TracingHook.Options(_createActivities, _includeVariant));
        }
    }

    /// <summary>
    /// TracingHook is a <see cref="Hook"/> that adds tracing capabilities to the LaunchDarkly SDK for feature flag
    /// evaluations.
    ///
    /// To create a TracingHook, see <see cref="TracingHookBuilder"/>.
    /// </summary>
    public class TracingHook : Hook
    {

        private static readonly AssemblyName AssemblyName = typeof(TracingHook).GetTypeInfo().Assembly.GetName();

        /// <summary>
        /// Used as the source of activities if the TracingHook is configured to create them.
        /// </summary>
        private static readonly ActivitySource Source = new ActivitySource(AssemblyName.Name,
            AssemblyName.Version.ToString());

        /// <summary>
        /// Returns the name of the ActivitySource that the TracingHook uses to generate Activities.
        /// </summary>
        public static string ActivitySourceName => Source.Name;

        private static class SemanticAttributes
        {
            public const string EventName = "feature_flag";
            public const string FeatureFlagKey = "feature_flag.key";
            public const string FeatureFlagProviderName = "feature_flag.provider_name";
            public const string FeatureFlagVariant = "feature_flag.variant";
            public const string FeatureFlagContextKeyAttributeName = "feature_flag.context.key";
        }

        internal struct Options
        {
            public bool CreateActivities { get; }
            public bool IncludeVariant { get; }

            public Options(bool createActivities, bool includeVariant)
            {
                CreateActivities = createActivities;
                IncludeVariant = includeVariant;
            }
        }

        private readonly Options _options;

        private const string ActivityFieldKey = "evalActivity";

        internal TracingHook(Options options) : base("LaunchDarkly Tracing Hook")
        {
            _options = options;
        }

        /// <summary>
        /// Returns a <see cref="TracingHookBuilder"/> which can be used to create a <see cref="TracingHook"/>.
        ///
        /// </summary>
        /// <returns>the builder</returns>
        public static TracingHookBuilder Builder() => new TracingHookBuilder();

        /// <summary>
        /// Returns the default TracingHook. By default, the hook will attach an event to the current activity.
        ///
        /// To change the configuration, see <see cref="Builder"/>.
        /// </summary>
        /// <returns></returns>
        public static TracingHook Default() => Builder().Build();

        /// <summary>
        /// Optionally creates a new Activity for the evaluation of a feature flag.
        /// </summary>
        /// <param name="context">the evaluation parameters</param>
        /// <param name="data">the series data</param>
        /// <returns>unchanged data if CreateActivities is disabled, or data containing a reference to the created activity</returns>
        public override SeriesData BeforeEvaluation(EvaluationSeriesContext context, SeriesData data)
        {
            if (!_options.CreateActivities) return data;

            var attrs = new ActivityTagsCollection
            {
                { SemanticAttributes.FeatureFlagKey, context.FlagKey },
                { SemanticAttributes.FeatureFlagContextKeyAttributeName, context.Context.FullyQualifiedKey }
            };

            // If there is a parent activity, then our new activity should be a child of it.
            // Otherwise, our new activity will be a root activity.
            var parentContext = Activity.Current?.Context ?? new ActivityContext();

            // This is an internal activity because LaunchDarkly SDK usage is an internal operation of an application.
            var activity = Source.StartActivity(context.Method, ActivityKind.Internal, parentContext, attrs);
            return new SeriesDataBuilder(data).Set(ActivityFieldKey, activity).Build();
        }

        /// <summary>
        /// Ends the activity created in BeforeEvaluation, if it exists. Adds the feature flag key, provider name, and context key
        /// to the existing activity. If IncludeVariant is enabled, also adds the variant.
        /// </summary>
        /// <param name="context">the evaluation parameters</param>
        /// <param name="data">the series data</param>
        /// <param name="detail">the evaluation details</param>
        /// <returns></returns>
        public override SeriesData AfterEvaluation(EvaluationSeriesContext context, SeriesData data, EvaluationDetail<LdValue> detail)
        {
            if (_options.CreateActivities && data.TryGetValue(ActivityFieldKey, out var value))
            {
                try
                {
                    var activity = (Activity) value;
                    activity?.Stop();
                }
                catch (System.InvalidCastException)
                {
                    // This should never happen, but if it does, don't crash the application.
                }
            }

            var attributes = new ActivityTagsCollection
            {
                {SemanticAttributes.FeatureFlagKey, context.FlagKey},
                {SemanticAttributes.FeatureFlagProviderName, "LaunchDarkly"},
                {SemanticAttributes.FeatureFlagContextKeyAttributeName, context.Context.FullyQualifiedKey},
            };

            if (_options.IncludeVariant)
            {
                attributes.Add(SemanticAttributes.FeatureFlagVariant, detail.Value.ToJsonString());
            }

            Activity.Current?.AddEvent(new ActivityEvent(name: SemanticAttributes.EventName, tags: attributes));
            return data;
        }
    }
}
