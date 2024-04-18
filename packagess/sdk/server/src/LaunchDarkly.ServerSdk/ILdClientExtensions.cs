﻿using LaunchDarkly.Sdk.Server.Interfaces;

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
    }
}
