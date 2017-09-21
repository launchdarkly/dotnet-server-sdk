using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using LaunchDarkly.EventSource;
using Newtonsoft.Json;


namespace LaunchDarkly.Client
{
    internal class StreamProcessor : IUpdateProcessor
    {
        private const String PUT = "put";
        private const String PATCH = "patch";
        private const String DELETE = "delete";
        private const String INDIRECT_PUT = "indirect/put";
        private const String INDIRECT_PATCH = "indirect/patch";
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
                readTimeout: TimeSpan.FromMilliseconds(1000 * 60 * 5),
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

        private void OnOpen(object sender, EventSource.StateChangedEventArgs e)
        {
        }

        private void OnClosed(object sender, EventSource.StateChangedEventArgs e)
        {
        }

        private void OnMessage(object sender, EventSource.MessageReceivedEventArgs e)
        {
            switch(e.EventName)
            {
                case PUT:
                    _featureStore.Init(JsonConvert.DeserializeObject<IDictionary<string, FeatureFlag>>(e.Message.Data));
                    if(Interlocked.CompareExchange(ref _initialized, INITIALIZED, UNINITIALIZED) == 0) {
                        _initTask.SetResult(true);
                        Logger.LogInformation("Initialized LaunchDarkly client.");
                    }
                    break;
                case PATCH:
                    //TODO
                    break;
                case DELETE:
                    //TODO
                    break;
                case INDIRECT_PUT:
                    //TODO
                    // try
                    // {   
                    //     _featureStore.Init(_featureRequestor.MakeAllRequestAsync()); 
                    //     if(Interlocked.CompareExchange(ref _initialized, INITIALIZED, UNINITIALIZED) == 0) {
                    //         _initTask.SetResult(true);
                    //         Logger.LogInformation("Initialized LaunchDarkly client.");
                    //     }
                    // }
                    // catch (Exception ex)
                    // {
                    //     Logger.LogError("Encountered exception in LaunchDarkly client", ex);
                    // }
                    break;
                case INDIRECT_PATCH:
                    //TODO
                    break;
            }
            Logger.LogInformation("EventSource Message Received. Event Name: {0}", e.EventName);
            Logger.LogInformation("EventSource Message Properties: {0}\tLast Event Id: {1}{0}\tOrigin: {2}{0}\tData: {3}",
                Environment.NewLine, e.Message.LastEventId, e.Message.Origin, e.Message.Data);
        }

        private void OnComment(object sender, EventSource.CommentReceivedEventArgs e)
        {
            Logger.LogDebug("Received a heartbeat.");
        }

        private void OnError(object sender, EventSource.ExceptionEventArgs e)
        {
            Logger.LogError("Encountered EventSource error:", e.Exception.Message);
            Logger.LogDebug("", e);
        }

        void IDisposable.Dispose()
        {
            Logger.LogInformation("Stopping LaunchDarkly StreamProcessor");
            _disposed = true;
        }
    }
}