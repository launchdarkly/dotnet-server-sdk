using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using LaunchDarkly.EventSource;


namespace LaunchDarkly.Client
{
    internal class StreamProcessor : IUpdateProcessor
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<StreamProcessor>();
        private static int UNINITIALIZED = 0;
        private static int INITIALIZED = 1;
        private readonly Configuration _config;
        private readonly FeatureRequestor _featureRequestor;
        private readonly IFeatureStore _featureStore;
        private int _initialized = UNINITIALIZED;
        private readonly TaskCompletionSource<bool> _initTask;
        private bool _disposed;
        private static EventSource.EventSource _es;

        internal StreamProcessor(Configuration config, FeatureRequestor featureRequestor, IFeatureStore featureStore)
        {
            _config = config;
            _featureRequestor = featureRequestor;
            _featureStore = featureStore;
            _initTask = new TaskCompletionSource<bool>();
        }

        bool IUpdateProcessor.Initialized()
        {
            return _initialized == INITIALIZED;
        }

        TaskCompletionSource<bool> IUpdateProcessor.Start()
        {
            Dictionary<string, string> headers = new Dictionary<string, string> {{"Authorization", _config.SdkKey}, {"User-Agent", "DotNetClient/" + Configuration.Version}, {"Accept", "text/event-stream"}};
            
            EventSource.Configuration config = new EventSource.Configuration(
                uri: new Uri(_config.StreamUri, "/flags"),
                connectionTimeOut: TimeSpan.FromSeconds(20),
                delayRetryDuration: TimeSpan.FromMilliseconds(1000),
                readTimeout: TimeSpan.FromMilliseconds(1000),
                requestHeaders: headers,
                logger: LdLogger.CreateLogger<EventSource.EventSource>()
            );      
            _es = new EventSource.EventSource(config);

            _es.Opened += OnOpen;
            _es.Closed += OnClosed;
            _es.CommentReceived += OnComment;
            _es.MessageReceived += OnMessage;
            _es.Error += OnError;

            try
            {
                _es.StartAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("General Exception: {0}", ex);
            }
            return _initTask;
        }

        private static void OnOpen(object sender, EventSource.StateChangedEventArgs e)
        {
            Logger.LogInformation("EventSource Opened. Current State: {0}", e.ReadyState);
        }

        private static void OnClosed(object sender, EventSource.StateChangedEventArgs e)
        {
            Logger.LogInformation("EventSource Closed. Current State {0}", e.ReadyState);
        }

        private static void OnMessage(object sender, EventSource.MessageReceivedEventArgs e)
        {
            Logger.LogInformation("EventSource Message Received. Event Name: {0}", e.EventName);
            Logger.LogInformation("EventSource Message Properties: {0}\tLast Event Id: {1}{0}\tOrigin: {2}{0}\tData: {3}",
                Environment.NewLine, e.Message.LastEventId, e.Message.Origin, e.Message.Data);
        }

        private static void OnComment(object sender, EventSource.CommentReceivedEventArgs e)
        {
            Logger.LogInformation("EventSource Comment Received: {0}", e.Comment);
        }

        private static void OnError(object sender, EventSource.ExceptionEventArgs e)
        {
            Logger.LogInformation("EventSource Error Occurred. Details: {0}", e.Exception.Message);
        }

        void IDisposable.Dispose()
        {
            Logger.LogInformation("Stopping LaunchDarkly StreamProcessor");
            _disposed = true;
        }
    }
}