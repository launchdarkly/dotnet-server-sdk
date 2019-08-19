using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Interface defining the public methods of <see cref="LdClient"/>.
    /// </summary>
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
        /// If the flag variation has a numeric value that is not an integer, it is rounded to the
        /// nearest integer. This rounding behavior may be changed in a future version of the SDK (for
        /// instance, to round down like the usual float-to-int conversion in C#), so you should avoid
        /// relying on it.
        ///
        /// If the flag variation does not have a numeric value, <c>defaultValue</c> is returned.
        /// 
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
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
        /// The <c>Reason</c> property in the result will also be included in analytics events, if you
        /// are capturing detailed event data for this flag.
        /// 
        /// The behavior is otherwise identical to <see cref="IntVariation"/>.
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <c>EvaluationDetail</c> object</returns>
        EvaluationDetail<int> IntVariationDetail(string key, User user, int defaultValue);

        /// <summary>
        /// Calculates the floating point numeric value of a feature flag for a given user.
        /// </summary>
        /// <remarks>
        /// If the flag variation does not have a numeric value, <c>defaultValue</c> is returned.
        /// 
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
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
        /// The <c>Reason</c> property in the result will also be included in analytics events, if you
        /// are capturing detailed event data for this flag.
        /// 
        /// The behavior is otherwise identical to <see cref="FloatVariation"/>.
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <c>EvaluationDetail</c> object</returns>
        EvaluationDetail<float> FloatVariationDetail(string key, User user, float defaultValue);

        /// <summary>
        /// Calculates value of a feature flag for a given user, as any JSON value type.
        /// </summary>
        /// <remarks>
        /// The Newtonsoft.Json type <see cref="JToken"/> is used to represent any of the value
        /// types that can exist in JSON.
        /// 
        /// Note that some subclasses of <c>JToken</c> are mutable: it is possible to modify values
        /// within a JSON array or a JSON object. Be careful not to modify the <c>JToken</c> that
        /// is returned by this method, since that could affect data structures inside the SDK. In
        /// a future version of the SDK, this will be replaced by an immutable type.
        /// 
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        JToken JsonVariation(string key, User user, JToken defaultValue);

        /// <summary>
        /// Calculates the value of a feature flag for a given user, as any JSON value type, and
        /// returns an object that describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// The <c>Reason</c> property in the result will also be included in analytics events, if you
        /// are capturing detailed event data for this flag.
        /// 
        /// The behavior is otherwise identical to <see cref="JsonVariation"/>.
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <c>EvaluationDetail</c> object</returns>
        EvaluationDetail<JToken> JsonVariationDetail(string key, User user, JToken defaultValue);

        /// <summary>
        /// Calculates the string value of a feature flag for a given user.
        /// </summary>
        /// <remarks>
        /// If the flag variation does not have a string value, <c>defaultValue</c> is returned.
        /// 
        /// Normally, the string value of a flag should not be null, since the LaunchDarkly UI
        /// does not allow you to assign a null value to a flag variation. However, since it may be
        /// possible to create a feature flag with a null variation by other means, and also since
        /// <c>defaultValue</c> is nullable, you should assume that the return value might be null.
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
        /// The <c>Reason</c> property in the result will also be included in analytics events, if you
        /// are capturing detailed event data for this flag.
        /// 
        /// The behavior is otherwise identical to <see cref="StringVariation"/>.
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <c>EvaluationDetail</c> object</returns>
        EvaluationDetail<string> StringVariationDetail(string key, User user, string defaultValue);

        /// <summary>
        /// Calculates the boolean value of a feature flag for a given user.
        /// </summary>
        /// <remarks>
        /// If the flag variation does not have a boolean value, <c>defaultValue</c> is returned.
        /// 
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        bool BoolVariation(string key, User user, bool defaultValue = false);

        /// <summary>
        /// Calculates theboolean  value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// The <c>Reason</c> property in the result will also be included in analytics events, if you
        /// are capturing detailed event data for this flag.
        /// 
        /// The behavior is otherwise identical to <see cref="BoolVariation"/>.
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <c>EvaluationDetail</c> object</returns>
        EvaluationDetail<bool> BoolVariationDetail(string key, User user, bool defaultValue);

        /// <summary>
        /// Registers the user.
        /// </summary>
        /// <param name="user">the user to register</param>
        void Identify(User user);

        /// <summary>
        /// Tracks that a user performed an event.
        /// </summary>
        /// <param name="name">the name of the event</param>
        /// <param name="user">the user that performed the event</param>
        void Track(string name, User user);

        /// <summary>
        /// Tracks that a user performed an event.
        /// </summary>
        /// <param name="name">the name of the event</param>
        /// <param name="user">the user that performed the event</param>
        /// <param name="data">a string containing additional data associated with the event, or null</param>
        void Track(string name, User user, string data);

        /// <summary>
        /// Tracks that a user performed an event.
        /// </summary>
        /// <param name="name">the name of the event</param>
        /// <param name="data">a JSON element containing additional data associated with the event, or null</param>
        /// <param name="user">the user that performed the event</param>
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
        /// <param name="data">a JSON element containing additional data associated with the event, or null</param>
        /// <param name="user">the user that performed the event</param>
        /// <param name="metricValue">A numeric value used by the LaunchDarkly experimentation feature in numeric custom
        /// metrics. This field will also be returned as part of the custom event for Data Export.</param>
        void Track(string name, JToken data, User user, double metricValue);

        /// <summary>
        /// Returns a map from feature flag keys to <see cref="JToken"/> feature flag values for a given user.
        /// If the result of a flag's evaluation would have returned the default variation, it will have a
        /// null entry in the map. If the client is offline, has not been initialized, or a null user or user
        /// with null/empty user key a <c>null</c> map will be returned. This method will not send
        /// analytics events back to LaunchDarkly.
        ///
        /// This method is deprecated; use AllFlagsState() instead.
        /// </summary>
        /// <param name="user">the end user requesting the feature flags</param>
        /// <returns>a map from feature flag keys to {@code JToken} for the specified user</returns>
        [Obsolete("Use AllFlagsState instead. Current versions of the client-side SDK will not generate analytics events correctly if you pass the result of AllFlags.")]
        IDictionary<string, JToken> AllFlags(User user);

        /// <summary>
        /// Returns an object that encapsulates the state of all feature flags for a given user, including the flag
        /// values and also metadata that can be used on the front end. This method does not send analytics events
        /// back to LaunchDarkly.
        ///
        /// The most common use case for this method is to bootstrap a set of client-side feature flags from
        /// a back-end service.
        /// </summary>
        /// <param name="user">the end user requesting the feature flags</param>
        /// <param name="options">optional <see cref="FlagsStateOption"/> values affecting how the state is
        /// computed  -  for instance, to filter the set of flags to only include the client-side-enabled ones</param>
        /// <returns>a <see cref="FeatureFlagsState"/> object (will never be null; see
        /// <see cref="FeatureFlagsState.Valid"/></returns>
        FeatureFlagsState AllFlagsState(User user, params FlagsStateOption[] options);

        /// <summary>
        /// For more info: <a href="https://github.com/launchdarkly/js-client#secure-mode">https://github.com/launchdarkly/js-client#secure-mode</a>
        /// </summary>
        /// <param name="user">the user to be hashed along with the SDK key</param>
        /// <returns>the hash, or null if the hash could not be calculated</returns>
        string SecureModeHash(User user);
    }
}