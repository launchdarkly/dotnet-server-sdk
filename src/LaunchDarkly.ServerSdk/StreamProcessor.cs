using System;
using System.Net.Http;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    internal class StreamProcessor : IDataSource, IStreamProcessor
    {
        private const String PUT = "put";
        private const String PATCH = "patch";
        private const String DELETE = "delete";
        private const String INDIRECT_PATCH = "indirect/patch";

        private static readonly ILog Log = LogManager.GetLogger(typeof(StreamProcessor));

        private readonly Configuration _config;
        private readonly StreamManager _streamManager;
        private readonly IFeatureRequestor _featureRequestor;
        private readonly IDataStore _dataStore;

        internal StreamProcessor(Configuration config, IFeatureRequestor featureRequestor,
            IDataStore dataStore, StreamManager.EventSourceCreator eventSourceCreator)
        {
            _streamManager = new StreamManager(this,
                MakeStreamProperties(config),
                config.StreamManagerConfiguration,
                ServerSideClientEnvironment.Instance,
                eventSourceCreator);
            _config = config;
            _featureRequestor = featureRequestor;
            _dataStore = dataStore;
        }

        private StreamProperties MakeStreamProperties(Configuration config)
        {
            return new StreamProperties(new Uri(config.StreamUri, "/all"),
                HttpMethod.Get, null);
        }

        #region IDataSource

        bool IDataSource.Initialized()
        {
            return _streamManager.Initialized;
        }

        Task<bool> IDataSource.Start()
        {
            return _streamManager.Start();
        }

        #endregion

        #region IStreamProcessor
        
        public async Task HandleMessage(StreamManager streamManager, string messageType, string messageData)
        {
            switch (messageType)
            {
                case PUT:
                    _dataStore.Init(JsonUtil.DecodeJson<PutData>(messageData).Data.ToGenericDictionary());
                    streamManager.Initialized = true;
                    break;
                case PATCH:
                    PatchData patchData = JsonUtil.DecodeJson<PatchData>(messageData);
                    string patchKey;
                    if (GetKeyFromPath(patchData.Path, VersionedDataKind.Features, out patchKey))
                    {
                        FeatureFlag flag = patchData.Data.ToObject<FeatureFlag>();
                        _dataStore.Upsert(VersionedDataKind.Features, flag);
                    }
                    else if (GetKeyFromPath(patchData.Path, VersionedDataKind.Segments, out patchKey))
                    {
                        Segment segment = patchData.Data.ToObject<Segment>();
                        _dataStore.Upsert(VersionedDataKind.Segments, segment);
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
                        _dataStore.Delete(VersionedDataKind.Features, deleteKey, deleteData.Version);
                    }
                    else if (GetKeyFromPath(deleteData.Path, VersionedDataKind.Segments, out deleteKey))
                    {
                        _dataStore.Delete(VersionedDataKind.Segments, deleteKey, deleteData.Version);
                    }
                    else
                    {
                        Log.WarnFormat("Received delete event with unknown path: {0}", deleteData.Path);
                    }
                    break;
                case INDIRECT_PATCH:
                    await UpdateTaskAsync(messageData);
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
                _featureRequestor.Dispose();
            }
        }

        private async Task UpdateTaskAsync(string objectPath)
        {
            try
            {
                if (GetKeyFromPath(objectPath, VersionedDataKind.Features, out var key))
                {
                    var feature = await _featureRequestor.GetFlagAsync(key);
                    if (feature != null)
                    {
                        _dataStore.Upsert(VersionedDataKind.Features, feature);
                    }
                }
                else if (GetKeyFromPath(objectPath, VersionedDataKind.Segments, out key))
                {
                    var segment = await _featureRequestor.GetSegmentAsync(key);
                    if (segment != null)
                    {
                        _dataStore.Upsert(VersionedDataKind.Segments, segment);
                    }
                }
                else
                {
                    Log.WarnFormat("Received indirect patch event with unknown path: {0}", objectPath);
                }
            }
            catch (AggregateException ex)
            {
                Log.ErrorFormat("Error Updating {0}: '{1}'",
                    ex, objectPath, Util.ExceptionMessage(ex.Flatten()));
            }
            catch (UnsuccessfulResponseException ex) when (ex.StatusCode == 401)
            {
                Log.ErrorFormat("Error Updating {0}: '{1}'", objectPath, Util.ExceptionMessage(ex));
                if (ex.StatusCode == 401)
                {
                    Log.Error("Received 401 error, no further streaming connection will be made since SDK key is invalid");
                    ((IDisposable)this).Dispose();
                }
            }
            catch (TimeoutException ex) {
                Log.ErrorFormat("Error Updating {0}: '{1}'",
                    ex, objectPath, Util.ExceptionMessage(ex));
                _streamManager.Restart();
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Error Updating feature: '{0}'",
                    ex, Util.ExceptionMessage(ex));
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
    }
}
