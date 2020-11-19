using System;
using System.Security.Cryptography;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// A client for the LaunchDarkly API. Client instances are thread-safe. Applications should instantiate
    /// a single <see cref="LdClient"/> for the lifetime of their application.
    /// </summary>
    public sealed class LdClient : IDisposable, ILdClient
    {
        private readonly Configuration _configuration;
        internal readonly IEventProcessor _eventProcessor;
        private readonly IDataStore _dataStore;
        internal readonly IDataSource _dataSource;
        internal readonly Evaluator _evaluator;
        private readonly Logger _log;

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
            _configuration = config;

            var logConfig = (config.LoggingConfigurationFactory ?? Components.Logging())
                .CreateLoggingConfiguration();
            _log = logConfig.LogAdapter.Logger(LogNames.Base);
            _log.Info("Starting LaunchDarkly Client {0}",
                ServerSideClientEnvironment.Instance.Version);

            var basicConfig = new BasicConfiguration(config.SdkKey, config.Offline, _log);

            ServerDiagnosticStore diagnosticStore = _configuration.DiagnosticOptOut ? null :
                new ServerDiagnosticStore(_configuration, basicConfig);

            var clientContext = new LdClientContext(basicConfig, config, diagnosticStore);

            _dataStore = (_configuration.DataStoreFactory ?? Components.InMemoryDataStore).CreateDataStore(clientContext);

            _evaluator = new Evaluator(GetFlag, GetSegment, _log);

            var eventProcessorFactory =
                config.Offline ? Components.NoEvents :
                (_configuration.EventProcessorFactory ?? Components.SendEvents());
            _eventProcessor = eventProcessorFactory.CreateEventProcessor(clientContext);

            var dataStoreUpdates = new DataStoreUpdates(_dataStore);
            IDataSourceFactory dataSourceFactory =
                config.Offline ? Components.ExternalUpdatesOnly :
                (_configuration.DataSourceFactory ?? Components.StreamingDataSource());
            _dataSource = dataSourceFactory.CreateDataSource(clientContext, dataStoreUpdates);

            var initTask = _dataSource.Start();

            if (!(_dataSource is ComponentsImpl.NullDataSource))
            {
                _log.Info("Waiting up to {0} milliseconds for LaunchDarkly client to start..",
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
                _log.Warn("AllFlagsState() was called when client is in offline mode. Returning empty state.");
                return new FeatureFlagsState(false);
            }
            if (!Initialized())
            {
                if (_dataStore.Initialized())
                {
                    _log.Warn("AllFlagsState() called before client initialized; using last known values from data store");
                }
                else
                {
                    _log.Warn("AllFlagsState() called before client initialized; data store unavailable, returning empty state");
                    return new FeatureFlagsState(false);
                }
            }
            if (user == null || user.Key == null)
            {
                _log.Warn("AllFlagsState() called with null user or null user key. Returning empty state");
                return new FeatureFlagsState(false);
            }

            var builder = new FeatureFlagsStateBuilder(options);
            var clientSideOnly = FlagsStateOption.HasOption(options, FlagsStateOption.ClientSideOnly);
            var withReasons = FlagsStateOption.HasOption(options, FlagsStateOption.WithReasons);
            var detailsOnlyIfTracked = FlagsStateOption.HasOption(options, FlagsStateOption.DetailsOnlyForTrackedFlags);
            KeyedItems<ItemDescriptor> flags = _dataStore.GetAll(DataKinds.Features);
            foreach (var pair in flags.Items)
            {
                if (pair.Value.Item is null || !(pair.Value.Item is FeatureFlag flag))
                {
                    continue;
                }
                if (clientSideOnly && !flag.ClientSide)
                {
                    continue;
                }
                try
                {
                    Evaluator.EvalResult result = _evaluator.Evaluate(flag, user, EventFactory.Default);
                    builder.AddFlag(flag.Key, result.Result.Value, result.Result.VariationIndex,
                        result.Result.Reason, flag.Version, flag.TrackEvents, flag.DebugEventsUntilDate);
                }
                catch (Exception e)
                {
                    _log.Error("Exception caught for feature flag \"{0}\" when evaluating all flags: {1}", flag.Key,
                        LogValues.ExceptionSummary(e));
                    _log.Debug(LogValues.ExceptionTrace(e));
                    EvaluationReason reason = EvaluationReason.ErrorReason(EvaluationErrorKind.EXCEPTION);
                    builder.AddFlag(flag.Key, new EvaluationDetail<LdValue>(LdValue.Null, null, reason));
                }
            }
            return builder.Build();
        }

        private EvaluationDetail<T> Evaluate<T>(string featureKey, User user, LdValue defaultValue, LdValue.Converter<T> converter,
            bool checkType, EventFactory eventFactory)
        {
            T defaultValueOfType = converter.ToType(defaultValue);
            if (!Initialized())
            {
                if (_dataStore.Initialized())
                {
                    _log.Warn("Flag evaluation before client initialized; using last known values from data store");
                }
                else
                {
                    _log.Warn("Flag evaluation before client initialized; data store unavailable, returning default value");
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.CLIENT_NOT_READY));
                }
            }

            FeatureFlag featureFlag = null;
            FeatureFlagEventProperties? flagEventProperties = null;
            try
            {
                featureFlag = GetFlag(featureKey);
                if (featureFlag == null)
                {
                    _log.Info("Unknown feature flag {0}; returning default value",
                        featureKey);
                    _eventProcessor.SendEvent(eventFactory.NewUnknownFeatureRequestEvent(featureKey, user, defaultValue,
                        EvaluationErrorKind.FLAG_NOT_FOUND));
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.FLAG_NOT_FOUND));
                }
                flagEventProperties = new FeatureFlagEventProperties(featureFlag);

                if (user == null || user.Key == null)
                {
                    _log.Warn("Feature flag evaluation called with null user or null user key. Returning default");
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
                        _log.Error("Expected type: {0} but got {1} when evaluating FeatureFlag: {2}. Returning default",
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
                _log.Error("Encountered exception in LaunchDarkly client: {0} when evaluating feature key: {1} for user key: {2}",
                     LogValues.ExceptionSummary(e),
                     featureKey,
                     user.Key);
                _log.Debug(LogValues.ExceptionTrace(e));
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
                _log.Warn("Track called with null user or null user key");
                return;
            }
            _eventProcessor.SendEvent(EventFactory.Default.NewCustomEvent(name, user, data));
        }

        /// <inheritdoc/>
        public void Track(string name, User user, LdValue data, double metricValue)
        {
            if (user == null || user.Key == null)
            {
                _log.Warn("Track called with null user or null user key");
            }
            _eventProcessor.SendEvent(EventFactory.Default.NewCustomEvent(name, user, data, metricValue));
        }

        /// <inheritdoc/>
        public void Identify(User user)
        {
            if (user == null || String.IsNullOrEmpty(user.Key))
            {
                _log.Warn("Identify called with null user or null user key");
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
                _log.Info("Closing LaunchDarkly client.");
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

        private FeatureFlag GetFlag(string key)
        {
            var maybeItem = _dataStore.Get(DataKinds.Features, key);
            if (maybeItem.HasValue && maybeItem.Value.Item != null && maybeItem.Value.Item is FeatureFlag f)
            {
                return f;
            }
            return null;
        }

        private Segment GetSegment(string key)
        {
            var maybeItem = _dataStore.Get(DataKinds.Segments, key);
            if (maybeItem.HasValue && maybeItem.Value.Item != null && maybeItem.Value.Item is Segment s)
            {
                return s;
            }
            return null;
        }
    }
}