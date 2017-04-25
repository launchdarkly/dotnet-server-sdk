using System;

namespace LaunchDarkly.Client
{

    internal static class Util
    {

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        internal static long GetUnixTimestampMillis(DateTime dateTime)
        {
            return (long) ( dateTime - UnixEpoch ).TotalMilliseconds;
        }

        internal static DateTime ObjectToDateTime(object value)
        {
            if(value is DateTime)
            {
                return ( (DateTime) value ).ToUniversalTime();
            }

            string sValue = value as string;
            if(sValue != null)
            {
                return DateTime.Parse(sValue).ToUniversalTime();
            }

            if(IsNumeric(value))
            {
                value = Convert.ToDouble(value);
            }
            // TODO: Change to DateTimeOffset.FromUnixTimeMilliseconds after migration to .Net 4.6
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((double) value);
        }

        internal static bool IsNumeric(object value)
        {
            Type type = value.GetType();
#if NET45
            if(!type.IsPrimitive)
            {
                return false;
            }
#endif
            TypeCode typeCode = Type.GetTypeCode(type);
            switch ( typeCode )
            {
                case TypeCode.Single:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        internal static string ExceptionMessage(Exception e)
        {
            var msg = e.Message;
            if(e.InnerException != null)
            {
                return msg + " with inner exception: " + e.InnerException.Message;
            }
            return msg;
        }

    }

}