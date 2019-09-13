using System;
using Newtonsoft.Json;

namespace LaunchDarkly.Client.Utils
{
    /// <summary>
    /// Helper methods that may be useful for implementing <see cref="IFeatureStore"/> or
    /// <see cref="IFeatureStoreCore"/>.
    /// </summary>
    public abstract class FeatureStoreHelpers
    {
        /// <summary>
        /// Unmarshals a feature store item from a JSON string. This is a convenience method for
        /// feature store implementations, so that they can use the same JSON library that is used
        /// within the LaunchDarkly SDK rather than importing one themselves. All of the storeable
        /// classes used by the SDK are guaranteed to support this type of deserialization.
        /// </summary>
        /// <typeparam name="T">class of the object that will be returned</typeparam>
        /// <param name="kind">specifies the type of item being decoded</param>
        /// <param name="data">the JSON string</param>
        /// <returns>the unmarshaled item</returns>
        /// <exception cref="UnmarshalException">if the string format is invalid</exception>
        public static T UnmarshalJson<T>(VersionedDataKind<T> kind, string data) where T : IVersionedData
        {
            try
            {
                return JsonUtil.DecodeJson<T>(data);
            }
            catch (JsonException e)
            {
                throw new UnmarshalException("Unable to unmarshal " + typeof(T).Name, e);
            }
        }

        /// <summary>
        /// Unmarshals a feature store item from a JSON string. This is a convenience method for
        /// feature store implementations, so that they can use the same JSON library that is used
        /// within the LaunchDarkly SDK rather than importing one themselves. All of the storeable
        /// classes used by the SDK are guaranteed to support this type of deserialization.
        /// 
        /// This is the same as the other UnmarshalJson method, except that it returns an
        /// <see cref="IVersionedData"/> rather than a more specific type. This is more likely
        /// to be useful if you are implementing <see cref="IFeatureStoreCore"/>.
        /// </summary>
        /// <param name="kind">specifies the type of item being decoded</param>
        /// <param name="data">the JSON string</param>
        /// <returns>the unmarshaled item</returns>
        /// <exception cref="UnmarshalException">if the string format is invalid</exception>
        public static IVersionedData UnmarshalJson(IVersionedDataKind kind, string data)
        {
            try
            {
                return (IVersionedData)JsonUtil.DecodeJson(data, kind.GetItemType());
            }
            catch (JsonException e)
            {
                throw new UnmarshalException("Unable to unmarshal " + kind.GetItemType().Name, e);
            }
        }

        /// <summary>
        /// Marshals a feature store item into a JSON string. This is a convenience method for
        /// feature store implementations, so that they can use the same JSON library that is used
        /// within the LaunchDarkly SDK rather than importing one themselves. All of the storeable
        /// classes used by the SDK are guaranteed to support this type of serialization.
        /// </summary>
        /// <param name="item">the item to be marshaled</param>
        /// <returns>the JSON string</returns>
        public static string MarshalJson(IVersionedData item)
        {
            return JsonUtil.EncodeJson(item);
        }
    }

    /// <summary>
    /// This exception is thrown by <see cref="FeatureStoreHelpers.UnmarshalJson(IVersionedDataKind, string)"/>
    /// if unmarshaling fails. It will normally contain an inner exception that is the underlying
    /// error from the deserialization method.
    /// </summary>
    public class UnmarshalException : Exception
    {
        /// <summary>
        /// Constructs an exception instance.
        /// </summary>
        /// <param name="message">the message</param>
        /// <param name="cause">the inner exception</param>
        public UnmarshalException(string message, Exception cause) : base(message, cause) { }
    }
}
