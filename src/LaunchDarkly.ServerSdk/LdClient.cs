using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Common.Logging;
using LaunchDarkly.Sdk.Interfaces;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Internal.Helpers;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Model;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// A client for the LaunchDarkly API. Client instances are thread-safe. Applications should instantiate
    /// a single <see cref="LdClient"/> for the lifetime of their application.
    /// </summary>
    public sealed class LdClient : IDisposable, ILdClient
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LdClient));

        private readonly Configuration _configuration;
        internal readonly IEventProcessor _eventProcessor;
        private readonly IDataStore _dataStore;
        internal readonly IDataSource _dataSource;
        internal readonly Evaluator _evaluator;

        /// <summary>
        /// Creates a new client to connect to LaunchDarkly with a custom configuration.
        /// </summary>
        /// <param name="config">a client configuration object</param>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .AllAttributesPrivate(true)
        ///         .EventCapacity(1000)
        ///         .Build();
        ///     var client = new LDClient(config);
        /// </code>
        /// </example>
        /// <remarks>
        /// The constructor will block until the client has successfully connected to LaunchDarkly
        /// (assuming it is not in <see cref="IConfigurationBuilder.Offline(bool)"/> mode), or until
        /// the timeout specified by <see cref="IConfigurationBuilder.StartWaitTime(TimeSpan)"/> has
        /// elapsed. If it times out, <see cref="LdClient.Initialized"/> will be false.
        /// </remarks>
        public LdClient(Configuration config)
        {
            Log.InfoFormat("Starting LaunchDarkly Client {0}",
                ServerSideClientEnvironment.Instance.Version);

            _configuration = config;
        
            IDataStore store = (_configuration.DataStoreFactory ?? Components.InMemoryDataStore).CreateDataStore();
            _dataStore = new DataStoreClientWrapper(store);

            _evaluator = new Evaluator(
                key => _dataStore.Get(VersionedDataKind.Features, key),
                key => _dataStore.Get(VersionedDataKind.Segments, key));

            ServerDiagnosticStore diagnosticStore = _configuration.DiagnosticOptOut ? null :
                new ServerDiagnosticStore(_configuration);
            
            var eventProcessorFactory = _configuration.EventProcessorFactory ?? Components.DefaultEventProcessor;
            if (eventProcessorFactory is IEventProcessorFactoryWithDiagnostics epfwd)
            {
                _eventProcessor = epfwd.CreateEventProcessor(_configuration, diagnosticStore);
            }
            else
            {
                _eventProcessor = eventProcessorFactory.CreateEventProcessor(_configuration);
            }
            
            var dataSourceFactory = _configuration.DataSourceFactory ?? Components.DefaultDataSource;
            if (dataSourceFactory is IDataSourceFactoryWithDiagnostics dsfwd)
            {
                _dataSource = dsfwd.CreateDataSource(_configuration, _dataStore, diagnosticStore);
            }
            else
            {
                _dataSource = dataSourceFactory.CreateDataSource(_configuration, _dataStore);
            }

            var initTask = _dataSource.Start();

            if (!(_dataSource is NullDataSource))
            {
                Log.InfoFormat("Waiting up to {0} milliseconds for LaunchDarkly client to start..",
                    _configuration.StartWaitTime.TotalMilliseconds);
            }

            try
            {
                var unused = initTask.Wait(_configuration.StartWaitTime);
            }
            catch (AggregateException)
            {
                // StreamProcessor may throw an exception if initialization fails, because we want that behavior
                // in the Xamarin client. However, for backward compatibility we do not want to throw exceptions
                // from the LdClient constructor in the .NET client, so we'll just swallow this.
            }
        }

        /// <summary>
        /// Creates a new client instance that connects to LaunchDarkly with the default configuration.
        /// </summary>
        /// <param name="sdkKey">the SDK key for your LaunchDarkly environment</param>
        /// <example>
        /// <code>
        ///     var client = new LDClient("my-sdk-key");
        /// </code>
        /// </example>
        /// <remarks>
        /// The constructor will block until the client has successfully connected to LaunchDarkly, or
        /// until the default timeout has elapsed (10 seconds). If it times out,
        /// <see cref="LdClient.Initialized"/> will be false.
        /// </remarks>
        public LdClient(string sdkKey) : this(Configuration.Default(sdkKey))
        {
        }

        /// <inheritdoc/>
        public bool Initialized()
        {
            return IsOffline() || _dataSource.Initialized();
        }

        /// <inheritdoc/>
        public bool IsOffline()
        {
            return _configuration.Offline;
        }

        /// <inheritdoc/>
        public bool BoolVariation(string key, User user, bool defaultValue = false)
        {
            return Evaluate(key, user, LdValue.Of(defaultValue), LdValue.Convert.Bool, true, EventFactory.Default).Value;
        }

        /// <inheritdoc/>
        public int IntVariation(string key, User user, int defaultValue)
        {
            return Evaluate(key, user, LdValue.Of(defaultValue), LdValue.Convert.Int, true, EventFactory.Default).Value;
        }

        /// <inheritdoc/>
        public float FloatVariation(string key, User user, float defaultValue)
        {
            return Evaluate(key, user, LdValue.Of(defaultValue), LdValue.Convert.Float, true, EventFactory.Default).Value;
        }

        /// <inheritdoc/>
        public string StringVariation(string key, User user, string defaultValue)
        {
            return Evaluate(key, user, LdValue.Of(defaultValue), LdValue.Convert.String, true, EventFactory.Default).Value;
        }
        
        /// <inheritdoc/>
        public LdValue JsonVariation(string key, User user, LdValue defaultValue)
        {
            return Evaluate(key, user, defaultValue, LdValue.Convert.Json, false, EventFactory.Default).Value;
        }

        /// <inheritdoc/>
        public EvaluationDetail<bool> BoolVariationDetail(string key, User user, bool defaultValue)
        {
            return Evaluate(key, user, LdValue.Of(defaultValue), LdValue.Convert.Bool, true, EventFactory.DefaultWithReasons);
        }

        /// <inheritdoc/>
        public EvaluationDetail<int> IntVariationDetail(string key, User user, int defaultValue)
        {
            return Evaluate(key, user, LdValue.Of(defaultValue), LdValue.Convert.Int, true, EventFactory.DefaultWithReasons);
        }

        /// <inheritdoc/>
        public EvaluationDetail<float> FloatVariationDetail(string key, User user, float defaultValue)
        {
            return Evaluate(key, user, LdValue.Of(defaultValue), LdValue.Convert.Float, true, EventFactory.DefaultWithReasons);
        }

        /// <inheritdoc/>
        public EvaluationDetail<string> StringVariationDetail(string key, User user, string defaultValue)
        {
            return Evaluate(key, user, LdValue.Of(defaultValue), LdValue.Convert.String, true, EventFactory.DefaultWithReasons);
        }
        
        /// <inheritdoc/>
        public EvaluationDetail<LdValue> JsonVariationDetail(string key, User user, LdValue defaultValue)
        {
            return Evaluate(key, user, defaultValue, LdValue.Convert.Json, false, EventFactory.DefaultWithReasons);
        }
        
        /// <inheritdoc/>
        public FeatureFlagsState AllFlagsState(User user, params FlagsStateOption[] options)
        {
            if (IsOffline())
            {
                Log.Warn("AllFlagsState() was called when client is in offline mode. Returning empty state.");
                return new FeatureFlagsState(false);
            }
            if (!Initialized())
            {
                if (_dataStore.Initialized())
                {
                    Log.Warn("AllFlagsState() called before client initialized; using last known values from data store");
                }
                else
                {
                    Log.Warn("AllFlagsState() called before client initialized; data store unavailable, returning empty state");
                    return new FeatureFlagsState(false);
                }
            }
            if (user == null || user.Key == null)
            {
                Log.Warn("AllFlagsState() called with null user or null user key. Returning empty state");
                return new FeatureFlagsState(false);
            }

            var state = new FeatureFlagsState(true);
            var clientSideOnly = FlagsStateOption.HasOption(options, FlagsStateOption.ClientSideOnly);
            var withReasons = FlagsStateOption.HasOption(options, FlagsStateOption.WithReasons);
            var detailsOnlyIfTracked = FlagsStateOption.HasOption(options, FlagsStateOption.DetailsOnlyForTrackedFlags);
            IDictionary<string, FeatureFlag> flags = _dataStore.All(VersionedDataKind.Features);
            foreach (KeyValuePair<string, FeatureFlag> pair in flags)
            {
                var flag = pair.Value;
                if (clientSideOnly && !flag.ClientSide)
                {
                    continue;
                }
                try
                {
                    Evaluator.EvalResult result = _evaluator.Evaluate(flag, user, EventFactory.Default);
                    state.AddFlag(flag, result.Result.Value, result.Result.VariationIndex,
                        withReasons ? (EvaluationReason?)result.Result.Reason : null, detailsOnlyIfTracked);
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("Exception caught for feature flag \"{0}\" when evaluating all flags: {1}", flag.Key, Util.ExceptionMessage(e));
                    Log.Debug(e.ToString(), e);
                    EvaluationReason reason = EvaluationReason.ErrorReason(EvaluationErrorKind.EXCEPTION);
                    state.AddFlag(flag, LdValue.Null, null, withReasons ? (EvaluationReason?)reason : null, detailsOnlyIfTracked);
                }
            }
            return state;
        }

        private EvaluationDetail<T> Evaluate<T>(string featureKey, User user, LdValue defaultValue, LdValue.Converter<T> converter,
            bool checkType, EventFactory eventFactory)
        {
            T defaultValueOfType = converter.ToType(defaultValue);
            if (!Initialized())
            {
                if (_dataStore.Initialized())
                {
                    Log.Warn("Flag evaluation before client initialized; using last known values from data store");
                }
                else
                {
                    Log.Warn("Flag evaluation before client initialized; data store unavailable, returning default value");
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.CLIENT_NOT_READY));
                }
            }

            FeatureFlag featureFlag = null;
            FeatureFlagEventProperties? flagEventProperties = null;
            try
            {
                featureFlag = _dataStore.Get(VersionedDataKind.Features, featureKey);
                if (featureFlag == null)
                {
                    Log.InfoFormat("Unknown feature flag {0}; returning default value",
                        featureKey);
                    _eventProcessor.SendEvent(eventFactory.NewUnknownFeatureRequestEvent(featureKey, user, defaultValue,
                        EvaluationErrorKind.FLAG_NOT_FOUND));
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.FLAG_NOT_FOUND));
                }
                flagEventProperties = new FeatureFlagEventProperties(featureFlag);

                if (user == null || user.Key == null)
                {
                    Log.Warn("Feature flag evaluation called with null user or null user key. Returning default");
                    _eventProcessor.SendEvent(eventFactory.NewDefaultFeatureRequestEvent(flagEventProperties, user, defaultValue,
                        EvaluationErrorKind.USER_NOT_SPECIFIED));
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.USER_NOT_SPECIFIED));
                }
                
                Evaluator.EvalResult evalResult = _evaluator.Evaluate(featureFlag, user, eventFactory);
                if (!IsOffline())
                {
                    foreach (var prereqEvent in evalResult.PrerequisiteEvents)
                    {
                        _eventProcessor.SendEvent(prereqEvent);
                    }
                }
                var evalDetail = evalResult.Result;
                EvaluationDetail<T> returnDetail;
                if (evalDetail.VariationIndex == null)
                {
                    returnDetail = new EvaluationDetail<T>(defaultValueOfType, null, evalDetail.Reason);
                    evalDetail = new EvaluationDetail<LdValue>(defaultValue, null, evalDetail.Reason);
                }
                else
                {
                    if (checkType && !defaultValue.IsNull && evalDetail.Value.Type != defaultValue.Type)
                    {
                        Log.ErrorFormat("Expected type: {0} but got {1} when evaluating FeatureFlag: {2}. Returning default",
                            defaultValue.Type,
                            evalDetail.Value.Type,
                            featureKey);

                        _eventProcessor.SendEvent(eventFactory.NewDefaultFeatureRequestEvent(flagEventProperties, user,
                            defaultValue, EvaluationErrorKind.WRONG_TYPE));
                        return new EvaluationDetail<T>(defaultValueOfType, null,
                            EvaluationReason.ErrorReason(EvaluationErrorKind.WRONG_TYPE));
                    }
                    returnDetail = new EvaluationDetail<T>(converter.ToType(evalDetail.Value),
                        evalDetail.VariationIndex, evalDetail.Reason);
                }
                _eventProcessor.SendEvent(eventFactory.NewFeatureRequestEvent(flagEventProperties, user,
                    evalDetail, defaultValue));
                return returnDetail;
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Encountered exception in LaunchDarkly client: {0} when evaluating feature key: {1} for user key: {2}",
                     Util.ExceptionMessage(e),
                     featureKey,
                     user.Key);
                Log.Debug(e.ToString(), e);
                var reason = EvaluationReason.ErrorReason(EvaluationErrorKind.EXCEPTION);
                if (featureFlag == null)
                {
                    _eventProcessor.SendEvent(eventFactory.NewUnknownFeatureRequestEvent(featureKey, user,
                        defaultValue, EvaluationErrorKind.EXCEPTION));
                }
                else
                {
                    _eventProcessor.SendEvent(eventFactory.NewFeatureRequestEvent(flagEventProperties, user,
                        new EvaluationDetail<LdValue>(defaultValue, null, reason), defaultValue));
                }
                return new EvaluationDetail<T>(defaultValueOfType, null, reason);
            }
        }
        
        /// <inheritdoc/>
        public string SecureModeHash(User user)
        {
            if (user == null || string.IsNullOrEmpty(user.Key))
            {
                return null;
            }
            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            byte[] keyBytes = encoding.GetBytes(_configuration.SdkKey);

            HMACSHA256 hmacSha256 = new HMACSHA256(keyBytes);
            byte[] hashedMessage = hmacSha256.ComputeHash(encoding.GetBytes(user.Key));
            return BitConverter.ToString(hashedMessage).Replace("-", "").ToLower();
        }

        /// <inheritdoc/>
        public void Track(string name, User user)
        {
            Track(name, user, LdValue.Null);
        }
        
        /// <inheritdoc/>
        public void Track(string name, User user, LdValue data)
        {
            if (user == null || String.IsNullOrEmpty(user.Key))
            {
                Log.Warn("Track called with null user or null user key");
                return;
            }
            _eventProcessor.SendEvent(EventFactory.Default.NewCustomEvent(name, user, data));
        }

        /// <inheritdoc/>
        public void Track(string name, User user, LdValue data, double metricValue)
        {
            if (user == null || user.Key == null)
            {
                Log.Warn("Track called with null user or null user key");
            }
            _eventProcessor.SendEvent(EventFactory.Default.NewCustomEvent(name, user, data, metricValue));
        }

        /// <inheritdoc/>
        public void Identify(User user)
        {
            if (user == null || String.IsNullOrEmpty(user.Key))
            {
                Log.Warn("Identify called with null user or null user key");
                return;
            }
            _eventProcessor.SendEvent(EventFactory.Default.NewIdentifyEvent(user));
        }

        /// <inheritdoc/>
        public Version Version
        {
            get
            {
                return ServerSideClientEnvironment.Instance.Version;
            }
        }
        
        private void Dispose(bool disposing)
        {
            if (disposing) // follow standard IDisposable pattern
            {
                Log.Info("Closing LaunchDarkly client.");
                _eventProcessor.Dispose();
                _dataStore.Dispose();
                _dataSource.Dispose();
            }
        }

        /// <summary>
        /// Shuts down the client and releases any resources it is using.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unless it is offline, the client will attempt to deliver any pending analytics events before
        /// closing.
        /// </para>
        /// <para>
        /// Any components that were added by specifying a factory object
        /// (<see cref="IConfigurationBuilder.DataStore(IDataStoreFactory)"/>, etc.)
        /// will also be disposed of by this method; their lifecycle is the same as the client's.
        /// </para>
        /// </remarks>
        /// <see cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        // Note that Flush, IsOffline, and Version are defined in ILdCommonClient, not in ILdClient. In
        // the next major version, the base interface will go away and they will move to ILdClient.

        /// <inheritdoc/>
        public void Flush()
        {
            _eventProcessor.Flush();
        }
    }
}