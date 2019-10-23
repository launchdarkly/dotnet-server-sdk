using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Interface defining the public methods of <see cref="LdClient"/>.
    /// </summary>
    /// <remarks>
    /// See also <see cref="ILdClientExtensions"/>, which provides convenience methods that build upon
    /// this interface.
    /// </remarks>
#pragma warning disable 618
    public interface ILdClient : ILdCommonClient
#pragma warning restore 618
    {
        /// <summary>
        /// Tests whether the client is ready to be used.
        /// </summary>
        /// <returns>true if the client is ready, or false if it is still initializing</returns>
        bool Initialized();

        /// <summary>
        /// Calculates the integer value of a feature flag for a given user.
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
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        int IntVariation(string key, User user, int defaultValue);

        /// <summary>
        /// Calculates the integer value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="IntVariation"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        EvaluationDetail<int> IntVariationDetail(string key, User user, int defaultValue);

        /// <summary>
        /// Calculates the floating point numeric value of a feature flag for a given user.
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
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        float FloatVariation(string key, User user, float defaultValue);

        /// <summary>
        /// Calculates the floating point numeric value of a feature flag for a given user, and returns
        /// an object that describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="FloatVariation"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        EvaluationDetail<float> FloatVariationDetail(string key, User user, float defaultValue);

        /// <summary>
        /// Calculates the value of a feature flag for a given user as any JSON value type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The Newtonsoft.Json type <see cref="JToken"/> is used to represent any of the value
        /// types that can exist in JSON. Note that some subclasses of <see cref="JToken"/> are mutable:
        /// it is possible to modify values within a JSON array or a JSON object. Be careful not to
        /// modify the <see cref="JToken"/> that is returned by this method, since that could affect
        /// data structures inside the SDK. The <see cref="LdValue"/> type avoids this
        /// problem, so it is better to use the <see cref="JsonVariation(string, User, LdValue)"/>
        /// overload that uses that type; in a future version of the SDK, these <see cref="JToken"/>-based
        /// methods will be removed.
        /// </para>
        /// <para>
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        [Obsolete("Use JsonVariation(string, User, ImmutableJsonValue)")]
        JToken JsonVariation(string key, User user, JToken defaultValue);

        /// <summary>
        /// Calculates the value of a feature flag for a given user as any JSON value type, and
        /// returns an object that describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The Newtonsoft.Json type <see cref="JToken"/> is used to represent any of the value
        /// types that can exist in JSON. Note that some subclasses of <see cref="JToken"/> are mutable:
        /// it is possible to modify values within a JSON array or a JSON object. Be careful not to
        /// modify the <see cref="JToken"/> that is returned by this method, since that could affect
        /// data structures inside the SDK. The <see cref="LdValue"/> type avoids this
        /// problem, so it is better to use the <see cref="JsonVariationDetail(string, User, LdValue)"/>
        /// overload that uses that type; in a future version of the SDK, these <see cref="JToken"/>-based
        /// methods will be removed.
        /// </para>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="JsonVariation(string, User, JToken)"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        [Obsolete("Use JsonVariationDetail(string, User, ImmutableJsonValue)")]
        EvaluationDetail<JToken> JsonVariationDetail(string key, User user, JToken defaultValue);

        /// <summary>
        /// Calculates the value of a feature flag for a given user as any JSON value type.
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
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        LdValue JsonVariation(string key, User user, LdValue defaultValue);

        /// <summary>
        /// Calculates the value of a feature flag for a given user as any JSON value type, and
        /// returns an object that describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="JsonVariationDetail(string, User, LdValue)"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        EvaluationDetail<LdValue> JsonVariationDetail(string key, User user, LdValue defaultValue);

        /// <summary>
        /// Calculates the string value of a feature flag for a given user.
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
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        string StringVariation(string key, User user, string defaultValue);

        /// <summary>
        /// Calculates the string value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="StringVariation"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        EvaluationDetail<string> StringVariationDetail(string key, User user, string defaultValue);

        /// <summary>
        /// Calculates the boolean value of a feature flag for a given user.
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
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        bool BoolVariation(string key, User user, bool defaultValue = false);

        /// <summary>
        /// Calculates the boolean value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvaluationDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// <para>
        /// The behavior is otherwise identical to <see cref="BoolVariation"/>.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        EvaluationDetail<bool> BoolVariationDetail(string key, User user, bool defaultValue);

        /// <summary>
        /// Registers the user.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method simply creates an analytics event containing the user properties, to
        /// that LaunchDarkly will know about that user if it does not already.
        /// </para>
        /// <para>
        /// Calling any evaluation method, such as <see cref="BoolVariation(string, User, bool)"/>,
        /// also sends the user information to LaunchDarkly (if events are enabled), so you only
        /// need to use <see cref="Identify(User)"/> if you want to identify the user without
        /// evaluating a flag.
        /// </para>
        /// <para>
        /// Note that event delivery is asynchronous, so the event may not actually be sent until
        /// later; see <see cref="LdClient.Flush"/>.
        /// </para>
        /// </remarks>
        /// <param name="user">the user to register</param>
        void Identify(User user);

        /// <summary>
        /// Tracks that a user performed an event.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method creates a "custom" analytics event containing the specified event name (key)
        /// and user properties. You may attach arbitrary data to the event by calling
        /// <see cref="Track(string, User, LdValue)"/> instead.
        /// </para>
        /// <para>
        /// Note that event delivery is asynchronous, so the event may not actually be sent until
        /// later; see <see cref="LdClient.Flush"/>.
        /// </para>
        /// </remarks>
        /// <param name="name">the name of the event</param>
        /// <param name="user">the user that performed the event</param>
        void Track(string name, User user);

        /// <summary>
        /// Tracks that a user performed an event (obsolete overload).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method creates a "custom" analytics event containing the specified event name (key),
        /// user properties, and optional custom data.
        /// </para>
        /// <para>
        /// Note that event delivery is asynchronous, so the event may not actually be sent until
        /// later; see <see cref="LdClient.Flush"/>.
        /// </para>
        /// </remarks>
        /// <param name="name">the name of the event</param>
        /// <param name="user">the user that performed the event</param>
        /// <param name="data">a string containing additional data associated with the event, or null</param>
        [Obsolete("Use Track(string, User, ImmutableJsonValue")]
        void Track(string name, User user, string data);

        /// <summary>
        /// Tracks that a user performed an event (obsolete overload).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method creates a "custom" analytics event containing the specified event name (key),
        /// user properties, and optional custom data.
        /// </para>
        /// <para>
        /// Note that event delivery is asynchronous, so the event may not actually be sent until
        /// later; see <see cref="LdClient.Flush"/>.
        /// </para>
        /// </remarks>
        /// <param name="name">the name of the event</param>
        /// <param name="data">a JSON element containing additional data associated with the event, or null</param>
        /// <param name="user">the user that performed the event</param>
        [Obsolete("Use Track(string, User, ImmutableJsonValue")]
        void Track(string name, JToken data, User user);
        // Note, the order of the parameters here is different than the other 3-parameter overload so that
        // passing null for data will not be an ambiguous method call.

        /// <summary>
        /// Tracks that a user performed an event, and provides an additional numeric value for custom metrics.
        /// </summary>
        /// <remarks>
        /// As of this version’s release date, the LaunchDarkly service does not support the <c>metricValue</c>
        /// parameter. As a result, calling this overload of <c>Track</c> will not yet produce any different
        /// behavior from calling <see cref="Track(string, JToken, User)"/> without a <c>metricValue</c>. Refer
        /// to the SDK reference guide for the latest status:
        /// https://docs.launchdarkly.com/docs/dotnet-sdk-reference#section-track
        /// </remarks>
        /// <param name="name">the name of the event</param>
        /// <param name="user">the user that performed the event</param>
        /// <param name="data">additional data associated with the event, or null</param>
        /// <param name="metricValue">A numeric value used by the LaunchDarkly experimentation feature in numeric custom
        /// metrics. This field will also be returned as part of the custom event for Data Export.</param>
        void Track(string name, User user, LdValue data, double metricValue);

        /// <summary>
        /// Tracks that a user performed an event.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method creates a "custom" analytics event containing the specified event name (key),
        /// user properties, and optional custom data. If you do not need custom data, pass
        /// <see cref="LdValue.Null"/> for the last parameter or simply omit the parameter.
        /// </para>
        /// <para>
        /// Note that event delivery is asynchronous, so the event may not actually be sent until
        /// later; see <see cref="LdClient.Flush"/>.
        /// </para>
        /// </remarks>
        /// <param name="name">the name of the event</param>
        /// <param name="data">additional data associated with the event, if any</param>
        /// <param name="user">the user that performed the event</param>
        void Track(string name, User user, LdValue data);

        /// <summary>
        /// Returns a map from feature flag keys to <see cref="JToken"/> feature flag values for a given user.
        /// Deprecated; use <see cref="AllFlagsState(User, FlagsStateOption[])"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the result of a flag's evaluation would have returned the default variation, it will have a
        /// null entry in the map. If the client is offline, has not been initialized, or a null user or user
        /// with null/empty user key a <c>null</c> map will be returned.
        /// </para>
        /// <para>
        /// This method will not send analytics events back to LaunchDarkly.
        /// </para>
        /// <para>
        /// This method is deprecated; use <see cref="AllFlagsState(User, FlagsStateOption[])"/> instead.
        /// </para>
        /// </remarks>
        /// <param name="user">the end user requesting the feature flags</param>
        /// <returns>a map from feature flag keys to {@code JToken} for the specified user</returns>
        [Obsolete("Use AllFlagsState instead. Current versions of the client-side SDK will not generate analytics events correctly if you pass the result of AllFlags.")]
        IDictionary<string, JToken> AllFlags(User user);

        /// <summary>
        /// Returns an object that encapsulates the state of all feature flags for a given user, which
        /// can be passed to front-end code.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The object returned by this method contains the flag values as well as other metadata that
        /// is used by the LaunchDarkly JavaScript client, so it can be used for
        /// <see href="https://docs.launchdarkly.com/docs/js-sdk-reference#section-bootstrapping">bootstrapping</see>.
        /// </para>
        /// <para>
        /// This method will not send analytics events back to LaunchDarkly.
        /// </para>
        /// </remarks>
        /// <param name="user">the end user requesting the feature flags</param>
        /// <param name="options">optional <see cref="FlagsStateOption"/> values affecting how the state is
        /// computed  -  for instance, to filter the set of flags to only include the client-side-enabled ones</param>
        /// <returns>a <see cref="FeatureFlagsState"/> object (will never be null; see
        /// <see cref="FeatureFlagsState.Valid"/></returns>
        FeatureFlagsState AllFlagsState(User user, params FlagsStateOption[] options);

        /// <summary>
        /// Creates a hash string that can be used by the JavaScript SDK to identify a user.
        /// </summary>
        /// <remarks>
        /// See <see href="https://docs.launchdarkly.com/docs/js-sdk-reference#section-secure-mode">Secure mode</see> in
        /// the JavaScript SDK Reference.
        /// </remarks>
        /// <param name="user">the user to be hashed along with the SDK key</param>
        /// <returns>the hash, or null if the hash could not be calculated</returns>
        string SecureModeHash(User user);
    }
}