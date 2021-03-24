using System;
using System.Net.Http;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Client.Interfaces;
using LaunchDarkly.Client.Utils;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    internal class StreamProcessor : IUpdateProcessor, IStreamProcessor
    {
        private const String PUT = "put";
        private const String PATCH = "patch";
        private const String DELETE = "delete";

        private static readonly ILog Log = LogManager.GetLogger(typeof(StreamProcessor));

        private readonly StreamManager _streamManager;
        private readonly IFeatureStore _featureStore;

        internal StreamProcessor(
            Configuration config,
            IFeatureStore featureStore,
            StreamManager.EventSourceCreator eventSourceCreator,
            IDiagnosticStore diagnosticStore,
            Uri baseUri,
            TimeSpan initialReconnectDelay
            )
        {
            var httpConfig = config.HttpConfiguration;
            var streamProperties = new StreamProperties(baseUri.AddPath("/all"),
                HttpMethod.Get, null);
            var streamManagerConfig = new StreamManagerConfigImpl
            {
                HttpAuthorizationKey = config.SdkKey,
                HttpClientHandler = httpConfig.MessageHandler as HttpClientHandler,
                HttpClientTimeout = httpConfig.ConnectTimeout,
                ReadTimeout = httpConfig.ReadTimeout,
                ReconnectTime = initialReconnectDelay,
                WrapperName = (httpConfig as IHttpConfigurationInternal)?.WrapperName,
                WrapperVersion = (httpConfig as IHttpConfigurationInternal)?.WrapperVersion
            };
            _streamManager = new StreamManager(this,
                streamProperties,
                streamManagerConfig,
                ServerSideClientEnvironment.Instance,
                eventSourceCreator, diagnosticStore);
            _featureStore = featureStore;
        }

        #region IUpdateProcessor

        bool IUpdateProcessor.Initialized()
        {
            return _streamManager.Initialized;
        }

        Task<bool> IUpdateProcessor.Start()
        {
            return _streamManager.Start();
        }

        #endregion

        #region IStreamProcessor

#pragma warning disable CS1998 // we know there are no awaits in this method
        public async Task HandleMessage(StreamManager streamManager, string messageType, string messageData)
#pragma warning restore CS1998
        {
            switch (messageType)
            {
                case PUT:
                    _featureStore.Init(JsonUtil.DecodeJson<PutData>(messageData).Data.ToGenericDictionary());
                    streamManager.Initialized = true;
                    break;
                case PATCH:
                    PatchData patchData = JsonUtil.DecodeJson<PatchData>(messageData);
                    string patchKey;
                    if (GetKeyFromPath(patchData.Path, VersionedDataKind.Features, out patchKey))
                    {
                        FeatureFlag flag = patchData.Data.ToObject<FeatureFlag>();
                        _featureStore.Upsert(VersionedDataKind.Features, flag);
                    }
                    else if (GetKeyFromPath(patchData.Path, VersionedDataKind.Segments, out patchKey))
                    {
                        Segment segment = patchData.Data.ToObject<Segment>();
                        _featureStore.Upsert(VersionedDataKind.Segments, segment);
                    }
                    else
                    {
                        Log.WarnFormat("Received patch event with unknown path: {0}", patchData.Path);
                    }
                    break;
                case DELETE:
                    DeleteData deleteData = JsonUtil.DecodeJson<DeleteData>(messageData);
                    string deleteKey;
                    if (GetKeyFromPath(deleteData.Path, VersionedDataKind.Features, out deleteKey))
                    {
                        _featureStore.Delete(VersionedDataKind.Features, deleteKey, deleteData.Version);
                    }
                    else if (GetKeyFromPath(deleteData.Path, VersionedDataKind.Segments, out deleteKey))
                    {
                        _featureStore.Delete(VersionedDataKind.Segments, deleteKey, deleteData.Version);
                    }
                    else
                    {
                        Log.WarnFormat("Received delete event with unknown path: {0}", deleteData.Path);
                    }
                    break;
            }
        }

        #endregion

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                ((IDisposable)_streamManager).Dispose();
            }
        }

        private bool GetKeyFromPath(string path, IVersionedDataKind kind, out string key)
        {
            if (path.StartsWith(kind.GetStreamApiPath()))
            {
                key = path.Substring(kind.GetStreamApiPath().Length);
                return true;
            }
            key = null;
            return false;
        }

        internal class PutData
        {
            internal AllData Data { get; private set; }

            [JsonConstructor]
            internal PutData(AllData data)
            {
                Data = data;
            }
        }

        internal class PatchData
        {
            internal string Path { get; private set; }
            internal JToken Data { get; private set; }

            [JsonConstructor]
            internal PatchData(string path, JToken data)
            {
                Path = path;
                Data = data;
            }
        }

        internal class DeleteData
        {
            internal string Path { get; private set; }
            internal int Version { get; private set; }

            [JsonConstructor]
            internal DeleteData(string path, int version)
            {
                Path = path;
                Version = version;
            }
        }

        internal struct StreamManagerConfigImpl : IStreamManagerConfiguration
        {
            public string HttpAuthorizationKey { get; internal set; }
            public HttpClientHandler HttpClientHandler { get; internal set; }
            public TimeSpan HttpClientTimeout { get; internal set; }
            public TimeSpan ReadTimeout { get; internal set; }
            public TimeSpan ReconnectTime { get; internal set; }
            public string WrapperName { get; internal set; }
            public string WrapperVersion { get; internal set; }
        }
    }
}
