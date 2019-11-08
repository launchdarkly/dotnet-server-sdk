using System;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Convenience methods that extend the <see cref="ILdClient"/> interface.
    /// </summary>
    /// <remarks>
    /// These are implemented outside of <see cref="ILdClient"/> and <see cref="LdClient"/> because they do not
    /// rely on any implementation details of <see cref="LdClient"/>; they are decorators that would work equally
    /// well with a stub or test implementation of the interface.
    /// </remarks>
    public static class ILdClientExtensions
    {
        /// <summary>
        /// Equivalent to <see cref="ILdClient.StringVariation(string, User, string)"/>, but converts the
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
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag (as an enum value)</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated or does not have a valid enum value</returns>
        public static T EnumVariation<T>(this ILdClient client, string key, User user, T defaultValue)
        {
            var stringVal = client.StringVariation(key, user, defaultValue.ToString());
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
        /// Equivalent to <see cref="ILdClient.StringVariationDetail(string, User, string)"/>, but converts the
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
        /// <param name="user">the end user requesting the flag</param>
        /// <param name="defaultValue">the default value of the flag (as an enum value)</param>
        /// <returns>an <see cref="EvaluationDetail{T}"/> object</returns>
        public static EvaluationDetail<T> EnumVariationDetail<T>(this ILdClient client, string key, User user, T defaultValue)
        {
            var stringDetail = client.StringVariationDetail(key, user, defaultValue.ToString());
            if (stringDetail.Value != null)
            {
                try
                {
                    var enumValue = (T)System.Enum.Parse(typeof(T), stringDetail.Value, true);
                    return new EvaluationDetail<T>(enumValue, stringDetail.VariationIndex, stringDetail.Reason);
                }
                catch (System.ArgumentException)
                {
                    return new EvaluationDetail<T>(defaultValue, stringDetail.VariationIndex, EvaluationReason.ErrorReason(EvaluationErrorKind.WRONG_TYPE));
                }
            }
            return new EvaluationDetail<T>(defaultValue, stringDetail.VariationIndex, stringDetail.Reason);
        }
    }
}
