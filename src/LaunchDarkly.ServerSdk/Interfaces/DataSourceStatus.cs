using System;
using System.IO;
using System.Text;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Information about the data source's status and about the last status change.
    /// </summary>
    /// <seealso cref="IDataSourceStatusProvider.Status"/>
    /// <seealso cref="IDataSourceStatusProvider.StatusChanged"/>
    public struct DataSourceStatus
    {
        /// <summary>
        /// An enumerated value representing the overall current state of the data source.
        /// </summary>
        public DataSourceState State { get; set; }

        /// <summary>
        /// The date/time that the value of <see cref="State"/> most recently changed.
        /// </summary>
        /// <remarks>
        /// The meaning of this depends on the current state:
        /// <list type="bullet">
        /// <item><description>For <see cref="DataSourceState.Initializing"/>, it is the time that the SDK started initializing.</description></item>
        /// <item><description>For <see cref="DataSourceState.Valid"/>, it is the time that the data source most recently entered a valid
        /// state, after previously having been either <see cref="DataSourceState.Initializing"/> or
        /// <see cref="DataSourceState.Interrupted"/>.</description></item>
        /// <item><description>For <see cref="DataSourceState.Interrupted"/>, it is the time that the data source most recently entered an
        /// error state, after previously having been <see cref="DataSourceState.Valid"/>.</description></item>
        /// <item><description>For <see cref="DataSourceState.Off"/>, it is the time that the data source encountered an unrecoverable error
        /// or that the SDK was explicitly shut down.</description></item>
        /// </list>
        /// </remarks>
        public DateTime StateSince { get; set; }

        /// <summary>
        /// Information about the last error that the data source encountered, if any.
        /// </summary>
        /// <remarks>
        /// This property should be updated whenever the data source encounters a problem, even if it does not cause
        /// <see cref="State"/> to change. For instance, if a stream connection fails and the state changes to
        /// <see cref="DataSourceState.Interrupted"/>, and then subsequent attempts to restart the connection also fail, the
        /// state will remain <see cref="DataSourceState.Interrupted"/> but the error information will be updated each time--
        /// and the last error will still be reported in this property even if the state later becomes
        /// <see cref="DataSourceState.Valid"/>.
        /// </remarks>
        public ErrorInfo? LastError { get; set; }

        /// <inheritdoc/>
        public override string ToString() =>
            string.Format("DataSourceStatus({0},{1},{2})", State, StateSince, LastError);

        /// <summary>
        /// A description of an error condition that the data source encountered.
        /// </summary>
        /// <seealso cref="DataSourceStatus.LastError"/>
        public struct ErrorInfo
        {
            /// <summary>
            /// An enumerated value representing the general category of the error.
            /// </summary>
            public ErrorKind Kind { get; set; }

            /// <summary>
            /// The HTTP status code if the error was <see cref="ErrorKind.ErrorResponse"/>, or zero otherwise.
            /// </summary>
            public int StatusCode { get; set; }

            /// <summary>
            /// Any additional human-readable information relevant to the error.
            /// </summary>
            /// <remarks>
            /// The format of this message is subject to change and should not be relied on programmatically.
            /// </remarks>
            public string Message { get; set; }

            /// <summary>
            /// The date/time that the error occurred.
            /// </summary>
            public DateTime Time { get; set; }

            /// <summary>
            /// Constructs an instance based on an exception.
            /// </summary>
            /// <param name="e">the exception</param>
            /// <returns>an ErrorInfo</returns>
            public static ErrorInfo FromException(Exception e) => new ErrorInfo
            {
                Kind = e is IOException ? ErrorKind.NetworkError : ErrorKind.Unknown,
                Message = e.Message,
                Time = DateTime.Now
            };

            /// <summary>
            /// Constructs an instance based on an HTTP error status.
            /// </summary>
            /// <param name="statusCode">the status code</param>
            /// <returns>an ErrorInfo</returns>
            public static ErrorInfo FromHttpError(int statusCode) => new ErrorInfo
            {
                Kind = ErrorKind.ErrorResponse,
                StatusCode = statusCode,
                Time = DateTime.Now
            };

            /// <inheritdoc/>
            public override string ToString()
            {
                var s = new StringBuilder();
                s.Append(Kind.Identifier());
                if (StatusCode > 0 || !string.IsNullOrEmpty(Message))
                {
                    s.Append("(");
                    if (StatusCode > 0)
                    {
                        s.Append(StatusCode);
                    }
                    if (!string.IsNullOrEmpty(Message))
                    {
                        if (StatusCode > 0)
                        {
                            s.Append(",");
                        }
                        s.Append(Message);
                    }
                    s.Append(")");
                }
                s.Append("@");
                s.Append(Time);
                return s.ToString();
            }
        }

        /// <summary>
        /// An enumeration describing the general type of an error reported in <see cref="ErrorInfo"/>.
        /// </summary>
        public enum ErrorKind
        {
            /// <summary>
            /// An unexpected error, such as an uncaught exception, further described by <see cref="ErrorInfo.Message"/>.
            /// </summary>
            Unknown,

            /// <summary>
            /// An I/O error such as a dropped connection.
            /// </summary>
            NetworkError,

            /// <summary>
            /// The LaunchDarkly service returned an HTTP response with an error status, available with
            /// <see cref="ErrorInfo.StatusCode"/>.
            /// </summary>
            ErrorResponse,

            /// <summary>
            /// The SDK received malformed data from the LaunchDarkly service.
            /// </summary>
            InvalidData,

            /// <summary>
            /// The data source itself is working, but when it tried to put an update into the data store, the data
            /// store failed (so the SDK may not have the latest data).
            /// </summary>
            /// <remarks>
            /// Data source implementations do not need to report this kind of error; it will be automatically
            /// reported by the SDK whenever one of the update methods of <see cref="IDataSourceUpdates"/> throws an
            /// exception.
            /// </remarks>
            StoreError
        }
    }

    /// <summary>
    /// An enumeration of possible values for <see cref="DataSourceStatus.State"/>.
    /// </summary>
    public enum DataSourceState
    {
        /// <summary>
        /// The initial state of the data source when the SDK is being initialized.
        /// </summary>
        /// <remarks>
        /// If it encounters an error that requires it to retry initialization, the state will remain at
        /// <see cref="Initializing"/> until it either succeeds and becomes <see cref="Valid"/>, or
        /// permanently fails and becomes <see cref="Off"/>.
        /// </remarks>
        Initializing,

        /// <summary>
        /// Indicates that the data source is currently operational and has not had any problems since the
        /// last time it received data.
        /// </summary>
        /// <remarks>
        /// In streaming mode, this means that there is currently an open stream connection and that at least
        /// one initial message has been received on the stream. In polling mode, it means that the last poll
        /// request succeeded.
        /// </remarks>
        Valid,

        /// <summary>
        /// Indicates that the data source encountered an error that it will attempt to recover from.
        /// </summary>
        /// <remarks>
        /// In streaming mode, this means that the stream connection failed, or had to be dropped due to some
        /// other error, and will be retried after a backoff delay. In polling mode, it means that the last poll
        /// request failed, and a new poll request will be made after the configured polling interval.
        /// </remarks>
        Interrupted,

        /// <summary>
        /// Indicates that the data source has been permanently shut down.
        /// </summary>
        /// <remarks>
        /// This could be because it encountered an unrecoverable error (for instance, the LaunchDarkly service
        /// rejected the SDK key; an invalid SDK key will never become valid), or because the SDK client was
        /// explicitly shut down.
        /// </remarks>
        Off
    }

    /// <summary>
    /// Extension helper methods for use with data source status types.
    /// </summary>
    public static class DataSourceStatusExtensions
    {
        /// <summary>
        /// Returns a standardized string identifier for a <see cref="DataSourceState"/>.
        /// </summary>
        /// <remarks>
        /// These Java-style uppercase identifiers (<c>INITIALIZING</c>, <c>VALID</c>, etc.) may be used in
        /// logging for consistency across SDKs.
        /// </remarks>
        /// <param name="state">a state value</param>
        /// <returns>a string identifier</returns>
        public static string Identifier(this DataSourceState state)
        {
            switch (state)
            {
                case DataSourceState.Initializing:
                    return "INITIALIZING";
                case DataSourceState.Valid:
                    return "VALID";
                case DataSourceState.Interrupted:
                    return "INTERRUPTED";
                case DataSourceState.Off:
                    return "OFF";
                default:
                    return state.ToString();
            }
        }

        /// <summary>
        /// Returns a standardized string identifier for a <see cref="DataSourceStatus.ErrorKind"/>.
        /// </summary>
        /// <remarks>
        /// These Java-style uppercase identifiers (<c>ERROR_RESPONSE</c>, <c>NETWORK_ERROR</c>, etc.) may be
        /// used in logging for consistency across SDKs.
        /// </remarks>
        /// <param name="errorKind">an error kind value</param>
        /// <returns>a string identifier</returns>
        public static string Identifier(this DataSourceStatus.ErrorKind errorKind)
        {
            switch (errorKind)
            {
                case DataSourceStatus.ErrorKind.ErrorResponse:
                    return "ERROR_RESPONSE";
                case DataSourceStatus.ErrorKind.InvalidData:
                    return "INVALID_DATA";
                case DataSourceStatus.ErrorKind.NetworkError:
                    return "NETWORK_ERROR";
                case DataSourceStatus.ErrorKind.StoreError:
                    return "STORE_ERROR";
                case DataSourceStatus.ErrorKind.Unknown:
                    return "UNKNOWN";
                default:
                    return errorKind.ToString();
            }
        }
    }
}
