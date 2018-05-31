using System;

namespace LaunchDarkly.Client
{
    internal class UnsuccessfulResponseException : Exception
    {
        public int StatusCode
        {
            get;
            private set;
        }

        internal UnsuccessfulResponseException(int statusCode) :
            base(string.Format("HTTP status {0}", statusCode))
        {
            StatusCode = statusCode;
        }
    }
}
