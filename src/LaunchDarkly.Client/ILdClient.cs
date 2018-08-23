using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Interface defining the public methods of <see cref="LdClient"/>.
    /// </summary>
    public interface ILdClient : ILdCommonClient
    {
        /// <summary>
        /// Tests whether the client is ready to be used.
        /// </summary>
        /// <returns>true if the client is ready, or false if it is still initializing</returns>
        bool Initialized();

        /// <summary>
        /// Calculates the integer value of a feature flag for a given user.
        /// </summary>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag is
        /// disabled in the LaunchDarkly control panel</returns>
        int IntVariation(string key, User user, int defaultValue);

        /// <summary>
        /// Calculates the integer value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined. The <c>Reason</c> property in the result will
        /// also be included in analytics events, if you are capturing detailed event data for this flag.
        /// </summary>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <c>EvaluationDetail</c> object</returns>
        EvaluationDetail<int> IntVariationDetail(string key, User user, int defaultValue);

        /// <summary>
        /// Calculates the floating point numeric value of a feature flag for a given user.
        /// </summary>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag is
        /// disabled in the LaunchDarkly control panel</returns>
        float FloatVariation(string key, User user, float defaultValue);

        /// <summary>
        /// Calculates the floating point numeric value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined. The <c>Reason</c> property in the result will
        /// also be included in analytics events, if you are capturing detailed event data for this flag.
        /// </summary>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <c>EvaluationDetail</c> object</returns>
        EvaluationDetail<float> FloatVariationDetail(string key, User user, float defaultValue);

        /// <summary>
        /// Calculates the <see cref="JToken"/> value of a feature flag for a given user.
        /// </summary>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag is
        /// disabled in the LaunchDarkly control panel</returns>
        JToken JsonVariation(string key, User user, JToken defaultValue);

        /// <summary>
        /// Calculates the <see cref="JToken"/> value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined. The <c>Reason</c> property in the result will
        /// also be included in analytics events, if you are capturing detailed event data for this flag.
        /// </summary>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <c>EvaluationDetail</c> object</returns>
        EvaluationDetail<JToken> JsonVariationDetail(string key, User user, JToken defaultValue);

        /// <summary>
        /// Calculates the string value of a feature flag for a given user.
        /// </summary>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag is
        /// disabled in the LaunchDarkly control panel</returns>
        string StringVariation(string key, User user, string defaultValue);

        /// <summary>
        /// Calculates the string value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined. The <c>Reason</c> property in the result will
        /// also be included in analytics events, if you are capturing detailed event data for this flag.
        /// </summary>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <c>EvaluationDetail</c> object</returns>
        EvaluationDetail<string> StringVariationDetail(string key, User user, string defaultValue);

        /// <summary>
        /// Calculates the value of a feature flag for a given user.
        /// </summary>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>whether or not the flag should be enabled, or <c>defaultValue</c> if the flag is
        /// disabled in the LaunchDarkly control panel</returns>
        bool BoolVariation(string key, User user, bool defaultValue = false);

        /// <summary>
        /// Calculates the value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined. The <c>Reason</c> property in the result will
        /// also be included in analytics events, if you are capturing detailed event data for this flag.
        /// </summary>
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
        /// Returns a map from feature flag keys to <see cref="JToken"/> feature flag values for a given user.
        /// If the result of a flag's evaluation would have returned the default variation, it will have a
        /// null entry in the map. If the client is offline, has not been initialized, or a null user or user
        /// with null/empty user key a <c>null</c> map will be returned. This method will not send
        /// analytics events back to LaunchDarkly.
        ///
        /// The most common use case for this method is to bootstrap a set of client-side feature flags from
        /// a back-end service.
        /// </summary>
        /// <param name="user">the end user requesting the feature flags</param>
        /// <returns>a map from feature flag keys to {@code JToken} for the specified user</returns>
        IDictionary<string, JToken> AllFlags(User user);

        /// <summary>
        /// For more info: <a href="https://github.com/launchdarkly/js-client#secure-mode">https://github.com/launchdarkly/js-client#secure-mode</a>
        /// </summary>
        /// <param name="user">the user to be hashed along with the SDK key</param>
        /// <returns>the hash, or null if the hash could not be calculated</returns>
        string SecureModeHash(User user);
    }
}