using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// Convenience methods that extend the <see cref="ILdClient"/> interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These allow you to do the following:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    ///     Treat a string-valued flag as if it referenced values of an <c>enum</c> type.
    /// </description></item>
    /// <item><description>
    ///     Call <see cref="ILdClient"/> methods with the <see cref="User"/> type instead of
    ///     <see cref="Context"/>. The SDK's preferred type for identifying an evaluation context,
    ///     when evaluating flags or generating analytics events, is <see cref="Context"/>; older
    ///     versions of the SDK used only the simpler <see cref="User"/> model. These extension
    ///     methods provide backward compatibility with application code that used the
    ///     <see cref="User"/> type. Each of them simply converts the User to a Context with
    ///     <see cref="Context.FromUser(User)"/> and calls the equivalent ILdClient method.
    ///     For instance, <c>client.BoolVariation(flagKey, user, false)</c> is exactly
    ///     equivalent to <c>client.BoolVariation(flagKey, Context.FromUser(user), false)</c>.
    /// </description></item>
    /// </list>
    /// <para>
    /// These are implemented outside of <see cref="ILdClient"/> and <see cref="LdClient"/> because they do not
    /// rely on any implementation details of <see cref="LdClient"/>; they are decorators that would work equally
    /// well with a stub or test implementation of the interface.
    /// </para>
    /// </remarks>
    public static class ILdClientExtensions
    {
        /// <summary>
        /// Equivalent to <see cref="ILdClient.StringVariation(string, Context, string)"/>, but converts the
        /// flag's string value to an enum value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the flag has a value that is not one of the allowed enum value names, or is not a string,
        /// <c>defaultValue</c> is returned.
        /// </para>
        /// <para>
        /// Note that there is no type constraint to guarantee that T really is an enum type, because that is
        /// a C# 7.3 feature that is unavailable in older versions of .NET Standard. If you try to use a
        /// non-enum type, you will simply receive the default value back.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">the enum type</typeparam>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag (as an enum value)</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated or does not have a valid enum value</returns>
        public static T EnumVariation<T>(this ILdClient client, string key, Context context, T defaultValue)
        {
            var stringVal = client.StringVariation(key, context, defaultValue.ToString());
            if (stringVal != null)
            {
                try
                {
                    return (T)System.Enum.Parse(typeof(T), stringVal, true);
                }
                catch (System.ArgumentException)
                { }
            }
            return defaultValue;
        }

        /// <summary>
        /// Equivalent to <see cref="ILdClient.StringVariation(string, Context, string)"/>, but converts the
        /// flag's string value to an enum value.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="EnumVariation{T}(ILdClient, string, Context, T)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <typeparam name="T">the enum type</typeparam>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag (as an enum value)</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated or does not have a valid enum value</returns>
        public static T EnumVariation<T>(this ILdClient client, string key, User user, T defaultValue) =>
            EnumVariation<T>(client, key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Equivalent to <see cref="ILdClient.StringVariationDetail(string, Context, string)"/>, but converts the
        /// flag's string value to an enum value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the flag has a value that is not one of the allowed enum value names, or is not a string,
        /// <c>defaultValue</c> is returned.
        /// </para>
        /// <para>
        /// Note that there is no type constraint to guarantee that T really is an enum type, because that is
        /// a C# 7.3 feature that is unavailable in older versions of .NET Standard. If you try to use a
        /// non-enum type, you will simply receive the default value back.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">the enum type</typeparam>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="context">the evaluation context</param>
        /// <param name="defaultValue">the default value of the flag (as an enum value)</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        public static EvaluationDetail<T> EnumVariationDetail<T>(this ILdClient client, string key, Context context, T defaultValue)
        {
            var stringDetail = client.StringVariationDetail(key, context, defaultValue.ToString());
            if (stringDetail.Value != null)
            {
                try
                {
                    var enumValue = (T)System.Enum.Parse(typeof(T), stringDetail.Value, true);
                    return new EvaluationDetail<T>(enumValue, stringDetail.VariationIndex, stringDetail.Reason);
                }
                catch (System.ArgumentException)
                {
                    return new EvaluationDetail<T>(defaultValue, stringDetail.VariationIndex, EvaluationReason.ErrorReason(EvaluationErrorKind.WrongType));
                }
            }
            return new EvaluationDetail<T>(defaultValue, stringDetail.VariationIndex, stringDetail.Reason);
        }

        /// <summary>
        /// Calculates the boolean value of a feature flag for a given user.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.BoolVariation(string, Context, bool)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="ILdClient.BoolVariation(string, Context, bool)"/>
        /// <seealso cref="BoolVariationDetail(ILdClient, string, User, bool)"/>
        public static bool BoolVariation(this ILdClient client,
            string key, User user, bool defaultValue = false) =>
            client.BoolVariation(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the boolean value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.BoolVariationDetail(string, Context, bool)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="ILdClient.BoolVariationDetail(string, Context, bool)"/>
        /// <seealso cref="BoolVariation(ILdClient, string, User, bool)"/>
        public static EvaluationDetail<bool> BoolVariationDetail(this ILdClient client,
            string key, User user, bool defaultValue) =>
            client.BoolVariationDetail(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the integer value of a feature flag for a given user.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.IntVariation(string, Context, int)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="ILdClient.IntVariation(string, Context, int)"/>
        /// <seealso cref="IntVariationDetail(ILdClient, string, User, int)"/>
        public static int IntVariation(this ILdClient client,
            string key, User user, int defaultValue) =>
            client.IntVariation(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the integer value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.IntVariationDetail(string, Context, int)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="ILdClient.IntVariationDetail(string, Context, int)"/>
        /// <seealso cref="IntVariation(ILdClient, string, User, int)"/>
        public static EvaluationDetail<int> IntVariationDetail(this ILdClient client,
            string key, User user, int defaultValue) =>
            client.IntVariationDetail(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the single-precision floating-point numeric value of a feature flag for a
        /// given user.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.FloatVariation(string, Context, float)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="ILdClient.FloatVariation(string, Context, float)"/>
        /// <seealso cref="FloatVariationDetail(ILdClient, string, User, float)"/>
        /// <seealso cref="DoubleVariation(ILdClient, string, User, double)"/>
        public static float FloatVariation(this ILdClient client,
            string key, User user, float defaultValue) =>
            client.FloatVariation(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the single-precision floating-point numeric value of a feature flag for a
        /// given user, and returns an object that describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.FloatVariationDetail(string, Context, float)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="ILdClient.FloatVariationDetail(string, Context, float)"/>
        /// <seealso cref="FloatVariation(ILdClient, string, User, float)"/>
        /// <seealso cref="DoubleVariationDetail(ILdClient, string, User, double)"/>
        public static EvaluationDetail<float> FloatVariationDetail(this ILdClient client,
            string key, User user, float defaultValue) =>
            client.FloatVariationDetail(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the double-precision floating-point numeric value of a feature flag for a
        /// given user.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.DoubleVariation(string, Context, double)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="ILdClient.DoubleVariation(string, Context, double)"/>
        /// <seealso cref="DoubleVariationDetail(ILdClient, string, User, double)"/>
        /// <seealso cref="FloatVariation(ILdClient, string, User, float)"/>
        public static double DoubleVariation(this ILdClient client,
            string key, User user, double defaultValue) =>
            client.DoubleVariation(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the double-precision floating-point numeric value of a feature flag for a
        /// given user, and returns an object that describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.DoubleVariationDetail(string, Context, double)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="ILdClient.DoubleVariationDetail(string, Context, double)"/>
        /// <seealso cref="DoubleVariation(ILdClient, string, User, double)"/>
        /// <seealso cref="FloatVariationDetail(ILdClient, string, User, float)"/>
        public static EvaluationDetail<double> DoubleVariationDetail(this ILdClient client,
            string key, User user, double defaultValue) =>
            client.DoubleVariationDetail(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the string value of a feature flag for a given user.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.StringVariation(string, Context, string)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="ILdClient.StringVariation(string, Context, string)"/>
        /// <seealso cref="StringVariationDetail(ILdClient, string, User, string)"/>
        public static string StringVariation(this ILdClient client,
            string key, User user, string defaultValue) =>
            client.StringVariation(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the string value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.StringVariationDetail(string, Context, string)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="ILdClient.StringVariationDetail(string, Context, string)"/>
        /// <seealso cref="StringVariation(ILdClient, string, User, string)"/>
        public static EvaluationDetail<string> StringVariationDetail(this ILdClient client,
            string key, User user, string defaultValue) =>
            client.StringVariationDetail(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the value of a feature flag for a given user as any JSON value type.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.JsonVariation(string, Context, LdValue)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given context, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="ILdClient.JsonVariation(string, Context, LdValue)"/>
        /// <seealso cref="JsonVariationDetail(ILdClient, string, User, LdValue)"/>
        public static LdValue JsonVariation(this ILdClient client,
            string key, User user, LdValue defaultValue) =>
            client.JsonVariation(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Calculates the value of a feature flag for a given user as any JSON value type, and
        /// returns an object that describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.JsonVariationDetail(string, Context, LdValue)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">the user attributes </param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        /// <seealso cref="ILdClient.JsonVariationDetail(string, Context, LdValue)"/>
        /// <seealso cref="JsonVariation(ILdClient, string, User, LdValue)"/>
        public static EvaluationDetail<LdValue> JsonVariationDetail(this ILdClient client,
            string key, User user, LdValue defaultValue) =>
            client.JsonVariationDetail(key, Context.FromUser(user), defaultValue);

        /// <summary>
        /// Reports details about a user.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.Identify(Context)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="user">the user; should not be null (a null reference will cause an error
        /// to be logged and no event will be sent</param>
        /// <seealso cref="ILdClient.Identify(Context)"/>
        public static void Identify(this ILdClient client, User user) =>
            client.Identify(Context.FromUser(user));

        /// <summary>
        /// Tracks that an application-defined event occurred.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.Track(string, Context)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="name">the name of the event</param>
        /// <param name="user">the user associated with the event; should not be null (a null reference
        /// will cause an error to be logged and no event will be sent</param>
        /// <seealso cref="ILdClient.Track(string, Context)"/>
        /// <seealso cref="Track(ILdClient, string, User, LdValue)"/>
        /// <seealso cref="Track(ILdClient, string, User, LdValue, double)"/>
        public static void Track(this ILdClient client, string name, User user) =>
            client.Track(name, Context.FromUser(user));

        /// <summary>
        /// Tracks that an application-defined event occurred.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.Track(string, Context, LdValue)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="name">the name of the event</param>
        /// <param name="user">the user associated with the event</param>
        /// <param name="data">additional data associated with the event, if any</param>
        /// <seealso cref="ILdClient.Track(string, Context, LdValue)"/>
        /// <seealso cref="Track(ILdClient, string, User)"/>
        /// <seealso cref="Track(ILdClient, string, User, LdValue, double)"/>
        public static void Track(this ILdClient client,
            string name, User user, LdValue data) =>
            client.Track(name, Context.FromUser(user), data);

        /// <summary>
        /// Tracks that an application-defined event occurred, and provides an additional numeric value for
        /// custom metrics.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.Track(string, Context, LdValue, double)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="name">the name of the event</param>
        /// <param name="user">the user associated with the event</param>
        /// <param name="data">additional data associated with the event; use <see cref="LdValue.Null"/> if
        /// not applicable</param>
        /// <param name="metricValue">a numeric value used by the LaunchDarkly experimentation feature in
        /// numeric custom metrics</param>
        /// <seealso cref="ILdClient.Track(string, Context, LdValue, double)"/>
        /// <seealso cref="Track(ILdClient, string, User)"/>
        /// <seealso cref="Track(ILdClient, string, User, LdValue)"/>
        public static void Track(this ILdClient client,
            string name, User user, LdValue data, double metricValue) =>
            client.Track(name, Context.FromUser(user), data, metricValue);

        /// <summary>
        /// Returns an object that encapsulates the state of all feature flags for a given user, which
        /// can be passed to front-end code.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.AllFlagsState(Context, FlagsStateOption[])"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="user">the user attributes</param>
        /// <param name="options">optional <see cref="FlagsStateOption"/> values affecting how the state is
        /// computed-- for instance, to filter the set of flags to only include the client-side-enabled ones</param>
        /// <returns>a <see cref="FeatureFlagsState"/> object (will never be null; see
        /// <see cref="FeatureFlagsState.Valid"/></returns>
        /// <seealso cref="ILdClient.AllFlagsState(Context, FlagsStateOption[])"/>
        public static FeatureFlagsState AllFlagsState(this ILdClient client,
            User user, params FlagsStateOption[] options) =>
            client.AllFlagsState(Context.FromUser(user), options);

        /// <summary>
        /// Creates a hash string that can be used by the JavaScript SDK to identify a user.
        /// </summary>
        /// <remarks>
        /// This is equivalent to <see cref="ILdClient.SecureModeHash(Context)"/>, but using the
        /// <see cref="User"/> type instead of <see cref="Context"/>.
        /// </remarks>
        /// <param name="client">the client instance</param>
        /// <param name="user">the user attributes</param>
        /// <returns>the hash, or null if the hash could not be calculated</returns>
        /// <seealso cref="ILdClient.SecureModeHash(Context)"/>
        public static string SecureModeHash(this ILdClient client, User user) =>
            client.SecureModeHash(Context.FromUser(user));
    }
}
