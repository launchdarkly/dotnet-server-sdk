using System;

namespace LaunchDarkly.Client
{
    internal class FeatureRequestorUnsuccessfulResponseException : Exception
    {
        public int StatusCode
        {
            get;
            private set;
        }

        internal FeatureRequestorUnsuccessfulResponseException(int statusCode) :
            base(string.Format("HTTP status {0}", statusCode))
        {
            StatusCode = statusCode;
        }
    }
}
