using System;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface defining the public methods of <see cref="LdClient"/>.
    /// </summary>
    /// <remarks>
    /// See also <see cref="ILdClientExtensions"/>, which provides convenience methods that build upon
    /// this interface. In particular, for every <see cref="ILdClient"/> method that takes a
    /// <see cref="Context"/> parameter, there is an extension method that allows you to pass the
    /// older <see cref="User"/> type instead.
    /// </remarks>
    public interface ILdClient
    {
        /// <summary>
        /// A mechanism for tracking the status of a Big Segment store.
        /// </summary>
        /// <remarks>
        /// The returned object has methods for checking whether the Big Segment store is (as far as the SDK
        /// knows) currently operational and tracking changes in this status. See
        /// <see cref="IBigSegmentStoreStatusProvider"/> for more about this functionality.
        /// </remarks>
        IBigSegmentStoreStatusProvider BigSegmentStoreStatusProvider { get; }

        /// <summary>
        /// A mechanism for tracking the status of the data source.
        /// </summary>
        /// <remarks>
        /// The data source is the mechanism that the SDK uses to get feature flag configurations, such as a
        /// streaming connection (the default) or poll requests. The <see cref="IDataSourceStatusProvider"/>
        /// has methods for checking whether the data source is (as far as the SDK knows) currently operational,
        /// and tracking changes in this status. This property will never be null.
        /// </remarks>
        IDataSourceStatusProvider DataSourceStatusProvider { get; }

        /// <summary>
        /// A mechanism for tracking the status of a persistent data store.
        /// </summary>
        /// <remarks>
        /// The <see cref="IDataStoreStatusProvider"/> has methods for checking whether the data store is (as
        /// far as the SDK knows) currently operational and tracking changes in this status. These are only
        /// relevant for a persistent data store; if you are using an in-memory data store, then this property
        /// is a stub object that always reports the store as operational.
        /// </remarks>
        IDataStoreStatusProvider DataStoreStatusProvider { get; }

        /// <summary>
        /// A mechanism for tracking changes in feature flag configurations.
        /// </summary>
        /// <remarks>
        /// The <see cref="IFlagTracker"/> contains methods for requesting notifications about feature flag
        /// changes using an event listener model.
        /// </remarks>
        IFlagTracker FlagTracker { get; }

        /// <summary>
        /// Tests whether the client is ready to be used.
        /// </summary>
        /// <value>true if the client is ready, or false if it is still initializing</value>
        bool Initialized { get; }

        /// <summary>
        /// Calculates the boolean value of a feature flag for a given context.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the flag variation does not have a boolean value, <c>defaultValue</c> is returned.
        /// </para>
        /// <para>
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="BoolVariationDetail(string, Context, bool)"/>
        /// <seealso cref="ILdClientExtensions.BoolVariation(ILdClient, string, User, bool)"/>
        bool BoolVariation(string key, Context context, bool defaultValue = false);

        /// <summary>
        /// Calculates the boolean value of a feature flag for a given context, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="BoolVariation(string, Context, bool)"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="BoolVariation(string, Context, bool)"/>
        /// <seealso cref="ILdClientExtensions.BoolVariationDetail(ILdClient, string, User, bool)"/>
        EvaluationDetail<bool> BoolVariationDetail(string key, Context context, bool defaultValue);

        /// <summary>
        /// Calculates the integer value of a feature flag for a given context.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the flag variation has a numeric value that is not an integer, it is rounded to the
        /// nearest integer. This rounding behavior may be changed in a future version of the SDK (for
        /// instance, to round toward zero like the usual float-to-int conversion in C#), so you should
        /// avoid relying on it.
        /// </para>
        /// <para>
        /// If the flag variation does not have a numeric value, <c>defaultValue</c> is returned.
        /// </para>
        /// <para>
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="IntVariationDetail(string, Context, int)"/>
        /// <seealso cref="ILdClientExtensions.IntVariation(ILdClient, string, User, int)"/>
        int IntVariation(string key, Context context, int defaultValue);

        /// <summary>
        /// Calculates the integer value of a feature flag for a given context, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="IntVariation(string, Context, int)"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="IntVariation(string, Context, int)"/>
        /// <seealso cref="ILdClientExtensions.IntVariationDetail(ILdClient, string, User, int)"/>
        EvaluationDetail<int> IntVariationDetail(string key, Context context, int defaultValue);

        /// <summary>
        /// Calculates the single-precision floating-point numeric value of a feature flag for a
        /// given context.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the flag variation does not have a numeric value, <c>defaultValue</c> is returned.
        /// </para>
        /// <para>
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="FloatVariationDetail(string, Context, float)"/>
        /// <seealso cref="DoubleVariation(string, Context, double)"/>
        /// <seealso cref="ILdClientExtensions.FloatVariation(ILdClient, string, User, float)"/>
        float FloatVariation(string key, Context context, float defaultValue);

        /// <summary>
        /// Calculates the single-precision floating-point numeric value of a feature flag for a
        /// given context, and returns an object that describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="FloatVariation(string, Context, float)"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="FloatVariation(string, Context, float)"/>
        /// <seealso cref="DoubleVariationDetail(string, Context, double)"/>
        /// <seealso cref="ILdClientExtensions.FloatVariationDetail(ILdClient, string, User, float)"/>
        EvaluationDetail<float> FloatVariationDetail(string key, Context context, float defaultValue);

        /// <summary>
        /// Calculates the double-precision floating-point numeric value of a feature flag for a
        /// given context.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the flag variation does not have a numeric value, <c>defaultValue</c> is returned.
        /// </para>
        /// <para>
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="DoubleVariationDetail(string, Context, double)"/>
        /// <seealso cref="FloatVariation(string, Context, float)"/>
        /// <seealso cref="ILdClientExtensions.DoubleVariation(ILdClient, string, User, double)"/>
        double DoubleVariation(string key, Context context, double defaultValue);

        /// <summary>
        /// Calculates the double-precision floating-point numeric value of a feature flag for a
        /// given context, and returns an object that describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="DoubleVariation(string, Context, double)"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="DoubleVariation(string, Context, double)"/>
        /// <seealso cref="FloatVariationDetail(string, Context, float)"/>
        /// <seealso cref="ILdClientExtensions.DoubleVariationDetail(ILdClient, string, User, double)"/>
        EvaluationDetail<double> DoubleVariationDetail(string key, Context context, double defaultValue);

        /// <summary>
        /// Calculates the string value of a feature flag for a given context.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the flag variation does not have a string value, <c>defaultValue</c> is returned.
        /// </para>
        /// <para>
        /// Normally, the string value of a flag should not be null, since the LaunchDarkly UI
        /// does not allow you to assign a null value to a flag variation. However, since it may be
        /// possible to create a feature flag with a null variation by other means, and also since
        /// <c>defaultValue</c> is nullable, you should assume that the return value might be null.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="StringVariationDetail(string, Context, string)"/>
        /// <seealso cref="ILdClientExtensions.StringVariation(ILdClient, string, User, string)"/>
        string StringVariation(string key, Context context, string defaultValue);

        /// <summary>
        /// Calculates the string value of a feature flag for a given context, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="StringVariation(string, Context, string)"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="StringVariation(string, Context, string)"/>
        /// <seealso cref="ILdClientExtensions.StringVariationDetail(ILdClient, string, User, string)"/>
        EvaluationDetail<string> StringVariationDetail(string key, Context context, string defaultValue);

        /// <summary>
        /// Calculates the value of a feature flag for a given context as any JSON value type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The type <see cref="LdValue"/> is used to represent any of the value types that can
        /// exist in JSON. Use <see cref="LdValue"/> methods to examine its type and value.
        /// </para>
        /// <para>
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="JsonVariationDetail(string, Context, LdValue)"/>
        /// <seealso cref="ILdClientExtensions.JsonVariation(ILdClient, string, User, LdValue)"/>
        LdValue JsonVariation(string key, Context context, LdValue defaultValue);

        /// <summary>
        /// Calculates the value of a feature flag for a given context as any JSON value type, and
        /// returns an object that describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="JsonVariationDetail(string, Context, LdValue)"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="JsonVariation(string, Context, LdValue)"/>
        /// <seealso cref="ILdClientExtensions.JsonVariationDetail(ILdClient, string, User, LdValue)"/>
        EvaluationDetail<LdValue> JsonVariationDetail(string key, Context context, LdValue defaultValue);

        /// <summary>
        /// Reports details about an evaluation context.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method simply creates an analytics event containing the context attributes, to
        /// that LaunchDarkly will know about that context if it does not already.
        /// </para>
        /// <para>
        /// Calling any evaluation method, such as <see cref="BoolVariation(string, Context, bool)"/>,
        /// also sends the context information to LaunchDarkly (if events are enabled), so you only
        /// need to use <see cref="Identify(Context)"/> if you want to identify the context without
        /// evaluating a flag.
        /// </para>
        /// <para>
        /// Note that event delivery is asynchronous, so the event may not actually be sent until
        /// later; see <see cref="LdClient.Flush"/>.
        /// </para>
        /// <para>
        /// For more information, see the
        /// <a href="https://docs.launchdarkly.com/sdk/features/identify#dotnet">Reference Guide</a>.
        /// </para>
        /// </remarks>
        /// <param name="context">the evaluation context</param>
        /// <seealso cref="ILdClientExtensions.Identify(ILdClient, User)"/>
        void Identify(Context context);

        /// <summary>
        /// Tracks that an application-defined event occurred.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method creates a "custom" analytics event containing the specified event name (key)
        /// and context attributes. You may attach arbitrary data to the event by calling
        /// <see cref="Track(string, Context, LdValue)"/> instead.
        /// </para>
        /// <para>
        /// Note that event delivery is asynchronous, so the event may not actually be sent until
        /// later; see <see cref="LdClient.Flush"/>.
        /// </para>
        /// </remarks>
        /// <param name="name">the name of the event</param>
        /// <param name="context">the evaluation context associated with the event</param>
        /// <seealso cref="Track(string, Context, LdValue)"/>
        /// <seealso cref="Track(string, Context, LdValue, double)"/>
        /// <seealso cref="ILdClientExtensions.Track(ILdClient, string, User)"/>
        void Track(string name, Context context);

        /// <summary>
        /// Tracks that an application-defined event occurred.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method creates a "custom" analytics event containing the specified event name (key),
        /// context properties, and optional custom data. If you do not need custom data, pass
        /// <see cref="LdValue.Null"/> for the last parameter or simply omit the parameter.
        /// </para>
        /// <para>
        /// Note that event delivery is asynchronous, so the event may not actually be sent until
        /// later; see <see cref="LdClient.Flush"/>.
        /// </para>
        /// </remarks>
        /// <param name="name">the name of the event</param>
        /// <param name="context">the evaluation context associated with the event</param>
        /// <param name="data">additional data associated with the event, if any</param>
        /// <seealso cref="Track(string, Context)"/>
        /// <seealso cref="Track(string, Context, LdValue, double)"/>
        /// <seealso cref="Track(string, Context, LdValue)"/>
        /// <seealso cref="ILdClientExtensions.Track(ILdClient, string, User, LdValue)"/>
        void Track(string name, Context context, LdValue data);

        /// <summary>
        /// Tracks that an application-defined event occurred, and provides an additional numeric value for
        /// custom metrics.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value is used by the LaunchDarkly experimentation feature in numeric custom metrics,
        /// and will also be returned as part of the custom event for Data Export.
        /// </para>
        /// <para>
        /// Note that event delivery is asynchronous, so the event may not actually be sent until
        /// later; see <see cref="LdClient.Flush"/>.
        /// </para>
        /// </remarks>
        /// <param name="name">the name of the event</param>
        /// <param name="context">the evaluation context associated with the event</param>
        /// <param name="data">additional data associated with the event; use <see cref="LdValue.Null"/> if
        /// not applicable</param>
        /// <param name="metricValue">a numeric value used by the LaunchDarkly experimentation feature in
        /// numeric custom metrics</param>
        /// <seealso cref="Track(string, Context)"/>
        /// <seealso cref="Track(string, Context, LdValue)"/>
        /// <seealso cref="ILdClientExtensions.Track(ILdClient, string, User, LdValue, double)"/>
        void Track(string name, Context context, LdValue data, double metricValue);

        /// <summary>
        /// Returns an object that encapsulates the state of all feature flags for a given context, which
        /// can be passed to front-end code.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The object returned by this method contains the flag values as well as other metadata that
        /// is used by the LaunchDarkly JavaScript client, so it can be used for
        /// <see href="https://docs.launchdarkly.com/sdk/features/bootstrapping#javascript">bootstrapping</see>.
        /// </para>
        /// <para>
        /// This method will not send analytics events back to LaunchDarkly.
        /// </para>
        /// </remarks>
        /// <param name="context">the evaluation context</param>
        /// <param name="options">optional <see cref="FlagsStateOption"/> values affecting how the state is
        /// computed-- for instance, to filter the set of flags to only include the client-side-enabled ones</param>
        /// <returns>a <see cref="FeatureFlagsState"/> object (will never be null; see
        /// <seealso cref="FeatureFlagsState.Valid"/></returns>
        /// <seealso cref="ILdClientExtensions.AllFlagsState(ILdClient, User, FlagsStateOption[])"/>
        FeatureFlagsState AllFlagsState(Context context, params FlagsStateOption[] options);

        /// <summary>
        /// Creates a hash string that can be used by the JavaScript SDK to identify a context.
        /// </summary>
        /// <remarks>
        /// See <see href="https://docs.launchdarkly.com/sdk/features/secure-mode#configuring-secure-mode-in-the-javascript-client-side-sdk">Secure mode</see> in
        /// the JavaScript SDK Reference.
        /// </remarks>
        /// <param name="context">the evaluation context</param>
        /// <returns>the hash, or null if the hash could not be calculated</returns>
        /// <seealso cref="ILdClientExtensions.SecureModeHash(ILdClient, User)"/>
        string SecureModeHash(Context context);

        /// <summary>
        /// Tells the client that all pending analytics events (if any) should be delivered as soon
        /// as possible. 
        /// </summary>
        /// <remarks>
        /// <para>
        /// This flush is asynchronous, so this method will return before it is complete. To wait for
        /// the flush to complete, use <see cref="FlushAndWait(TimeSpan)"/> instead (or, if you are done
        /// with the SDK, <see cref="LdClient.Dispose()"/>).
        /// </para>
        /// <para>
        /// For more information, see: <a href="https://docs.launchdarkly.com/sdk/features/flush#net-server-side">
        /// Flushing Events</a>.
        /// </para>
        /// </remarks>
        /// <seealso cref="FlushAndWait(TimeSpan)"/>
        void Flush();

        /// <summary>
        /// Tells the client to deliver any pending analytics events synchronously now.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unlike <see cref="Flush"/>, this method waits for event delivery to finish. The timeout parameter, if
        /// greater than zero, specifies the maximum amount of time to wait. If the timeout elapses before
        /// delivery is finished, the method returns early and returns false; in this case, the SDK may still
        /// continue trying to deliver the events in the background.
        /// </para>
        /// <para>
        /// If the timeout parameter is zero or negative, the method waits as long as necessary to deliver the
        /// events. However, the SDK does not retry event delivery indefinitely; currently, any network error
        /// or server error will cause the SDK to wait one second and retry one time, after which the events
        /// will be discarded so that the SDK will not keep consuming more memory for events indefinitely.
        /// </para>
        /// <para>
        /// The method returns true if event delivery either succeeded, or definitively failed, before the
        /// timeout elapsed. It returns false if the timeout elapsed.
        /// </para>
        /// <para>
        /// This method is also implicitly called if you call <see cref="LdClient.Dispose()"/>. The difference is
        /// that FlushAndWait does not shut down the SDK client.
        /// </para>
        /// <para>
        /// For more information, see: <a href="https://docs.launchdarkly.com/sdk/features/flush#net-server-side">
        /// Flushing Events</a>.
        /// </para>
        /// </remarks>
        /// <param name="timeout">the maximum time to wait</param>
        /// <returns>true if completed, false if timed out</returns>
        /// <seealso cref="Flush"/>
        bool FlushAndWait(TimeSpan timeout);
    }
}
