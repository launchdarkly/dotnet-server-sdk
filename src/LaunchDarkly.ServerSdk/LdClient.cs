using System;
using System.Security.Cryptography;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.BigSegments;
using LaunchDarkly.Sdk.Server.Internal.DataSources;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Evaluation;
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
        #region Private fields

        private readonly IBigSegmentStoreStatusProvider _bigSegmentStoreStatusProvider;
        private readonly BigSegmentStoreWrapper _bigSegmentStoreWrapper;
        private readonly Configuration _configuration;
        internal readonly IEventProcessor _eventProcessor;
        private readonly IDataStore _dataStore;
        internal readonly IDataSource _dataSource;
        private readonly DataSourceStatusProviderImpl _dataSourceStatusProvider;
        private readonly DataStoreStatusProviderImpl _dataStoreStatusProvider;
        private readonly IFlagTracker _flagTracker;
        internal readonly Evaluator _evaluator;
        private readonly Logger _log;
        private readonly Logger _evalLog;

        #endregion

        #region Public properties

        /// <inheritdoc/>
        public IBigSegmentStoreStatusProvider BigSegmentStoreStatusProvider => _bigSegmentStoreStatusProvider;

        /// <inheritdoc/>
        public IDataSourceStatusProvider DataSourceStatusProvider => _dataSourceStatusProvider;

        /// <inheritdoc/>
        public IDataStoreStatusProvider DataStoreStatusProvider => _dataStoreStatusProvider;

        /// <inheritdoc/>
        public IFlagTracker FlagTracker => _flagTracker;

        /// <inheritdoc/>
        public bool Initialized => _dataSource.Initialized;

        /// <inheritdoc/>
        public Version Version => AssemblyVersions.GetAssemblyVersionForType(typeof(LdClient));

        #endregion

        #region Public constructors

        /// <summary>
        /// Creates a new client to connect to LaunchDarkly with a custom configuration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Applications should instantiate a single instance for the lifetime of the application. In
        /// unusual cases where an application needs to evaluate feature flags from different LaunchDarkly
        /// projects or environments, you may create multiple clients, but they should still be retained
        /// for the lifetime of the application rather than created per request or per thread.
        /// </para>
        /// <para>
        /// Normally, the client will begin attempting to connect to LaunchDarkly as soon as you call the
        /// constructor. The constructor returns as soon as any of the following things has happened:
        /// </para>
        /// <list type="number">
        /// <item><description> It has successfully connected to LaunchDarkly and received feature flag data. In this
        /// case, <see cref="Initialized"/> will be true, and the <see cref="DataSourceStatusProvider"/>
        /// will return a state of <see cref="DataSourceState.Valid"/>. </description></item>
        /// <item><description> It has not succeeded in connecting within the <see cref="ConfigurationBuilder.StartWaitTime(TimeSpan)"/>
        /// timeout (the default for this is 5 seconds). This could happen due to a network problem or a
        /// temporary service outage. In this case, <see cref="Initialized"/> will be false, and the
        /// <see cref="DataSourceStatusProvider"/> will return a state of <see cref="DataSourceState.Initializing"/>,
        /// indicating that the SDK will still continue trying to connect in the background. </description></item>
        /// <item><description> It has encountered an unrecoverable error: for instance, LaunchDarkly has rejected the
        /// SDK key. Since an invalid key will not become valid, the SDK will not retry in this case.
        /// <see cref="Initialized"/> will be false, and the <see cref="DataSourceStatusProvider"/> will
        /// return a state of <see cref="DataSourceState.Off"/>. </description></item>
        /// </list>
        /// <para>
        /// If you have specified <see cref="ConfigurationBuilder.Offline"/> mode or
        /// <see cref="Components.ExternalUpdatesOnly"/>, the constructor returns immediately without
        /// trying to connect to LaunchDarkly.
        /// </para>
        /// <para>
        /// Failure to connect to LaunchDarkly will never cause the constructor to throw an exception.
        /// Under any circumstance where it is not able to get feature flag data from LaunchDarkly (and
        /// therefore <see cref="Initialized"/> is false), if it does not have any other source of data
        /// (such as a persistent data store) then feature flag evaluations will behave the same as if
        /// the flags were not found: that is, they will return whatever default value is specified in
        /// your code.
        /// </para>
        /// </remarks>
        /// <param name="config">a client configuration object (which includes an SDK key)</param>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .AllAttributesPrivate(true)
        ///         .EventCapacity(1000)
        ///         .Build();
        ///     var client = new LDClient(config);
        /// </code>
        /// </example>
        /// <seealso cref="LdClient.LdClient(string)"/>
        public LdClient(Configuration config)
        {
            _configuration = config;

            var logConfig = (config.LoggingConfigurationFactory ?? Components.Logging())
                .CreateLoggingConfiguration();
            _log = logConfig.LogAdapter.Logger(logConfig.BaseLoggerName ?? LogNames.DefaultBase);
            _log.Info("Starting LaunchDarkly client {0}",
                AssemblyVersions.GetAssemblyVersionStringForType(typeof(LdClient)));
            _evalLog = _log.SubLogger(LogNames.EvaluationSubLog);

            var basicConfig = new BasicConfiguration(config, _log);
            var httpConfig = (config.HttpConfigurationFactory ?? Components.HttpConfiguration())
                .CreateHttpConfiguration(basicConfig);
            ServerDiagnosticStore diagnosticStore = _configuration.DiagnosticOptOut ? null :
                new ServerDiagnosticStore(_configuration, basicConfig, httpConfig);

            var taskExecutor = new TaskExecutor(this, _log);

            var clientContext = new LdClientContext(basicConfig, httpConfig, diagnosticStore, taskExecutor);

            var dataStoreUpdates = new DataStoreUpdatesImpl(taskExecutor, _log.SubLogger(LogNames.DataStoreSubLog));
            _dataStore = (_configuration.DataStoreFactory ?? Components.InMemoryDataStore)
                .CreateDataStore(clientContext, dataStoreUpdates);
            _dataStoreStatusProvider = new DataStoreStatusProviderImpl(_dataStore, dataStoreUpdates);

            var bigSegmentsConfig = (_configuration.BigSegmentsConfigurationFactory ?? Components.BigSegments(null))
                .CreateBigSegmentsConfiguration(clientContext);
            _bigSegmentStoreWrapper = bigSegmentsConfig.Store is null ? null :
                new BigSegmentStoreWrapper(
                    bigSegmentsConfig,
                    taskExecutor,
                    _log.SubLogger(LogNames.BigSegmentsSubLog)
                    );
            _bigSegmentStoreStatusProvider = new BigSegmentStoreStatusProviderImpl(_bigSegmentStoreWrapper);

            _evaluator = new Evaluator(
                GetFlag,
                GetSegment,
                _bigSegmentStoreWrapper == null ? (Func<string, BigSegmentsInternalTypes.BigSegmentsQueryResult>)null :
                    _bigSegmentStoreWrapper.GetUserMembership,
                _log
                );

            var eventProcessorFactory =
                config.Offline ? Components.NoEvents :
                (_configuration.EventProcessorFactory ?? Components.SendEvents());
            _eventProcessor = eventProcessorFactory.CreateEventProcessor(clientContext);

            var dataSourceUpdates = new DataSourceUpdatesImpl(_dataStore, _dataStoreStatusProvider,
                taskExecutor, _log, logConfig.LogDataSourceOutageAsErrorAfter);
            IDataSourceFactory dataSourceFactory =
                config.Offline ? Components.ExternalUpdatesOnly :
                (_configuration.DataSourceFactory ?? Components.StreamingDataSource());
            _dataSource = dataSourceFactory.CreateDataSource(clientContext, dataSourceUpdates);
            _dataSourceStatusProvider = new DataSourceStatusProviderImpl(dataSourceUpdates);
            _flagTracker = new FlagTrackerImpl(dataSourceUpdates,
                (string key, User user) => JsonVariation(key, user, LdValue.Null));

            var initTask = _dataSource.Start();

            if (!(_dataSource is ComponentsImpl.NullDataSource))
            {
                _log.Info("Waiting up to {0} milliseconds for LaunchDarkly client to start...",
                    _configuration.StartWaitTime.TotalMilliseconds);
            }

            try
            {
                var success = initTask.Wait(_configuration.StartWaitTime);
                if (!success)
                {
                    _log.Warn("Timeout encountered waiting for LaunchDarkly client initialization");
                }
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
        /// <remarks>
        /// <para>
        /// If you need to specify any custom SDK options, use <see cref="LdClient.LdClient(Configuration)"/>
        /// instead.
        /// </para>
        /// <para>
        /// Applications should instantiate a single instance for the lifetime of the application. In
        /// unusual cases where an application needs to evaluate feature flags from different LaunchDarkly
        /// projects or environments, you may create multiple clients, but they should still be retained
        /// for the lifetime of the application rather than created per request or per thread.
        /// </para>
        /// <para>
        /// The constructor will never throw an exception, even if initialization fails. For more details
        /// about initialization behavior and how to detect error conditions, see
        /// <see cref="LdClient.LdClient(Configuration)"/>.
        /// </para>
        /// </remarks>
        /// <param name="sdkKey">the SDK key for your LaunchDarkly environment</param>
        /// <seealso cref="LdClient.LdClient(Configuration)"/>
        public LdClient(string sdkKey) : this(Configuration.Default(sdkKey))
        {
        }

        #endregion

        #region Public methods

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
        public double DoubleVariation(string key, User user, double defaultValue)
        {
            return Evaluate(key, user, LdValue.Of(defaultValue), LdValue.Convert.Double, true, EventFactory.Default).Value;
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
        public EvaluationDetail<double> DoubleVariationDetail(string key, User user, double defaultValue)
        {
            return Evaluate(key, user, LdValue.Of(defaultValue), LdValue.Convert.Double, true, EventFactory.DefaultWithReasons);
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
                _evalLog.Warn("AllFlagsState() called when client is in offline mode; returning empty state");
                return new FeatureFlagsState(false);
            }
            if (!Initialized)
            {
                if (_dataStore.Initialized())
                {
                    _evalLog.Warn("AllFlagsState() called before client initialized; using last known values from data store");
                }
                else
                {
                    _evalLog.Warn("AllFlagsState() called before client initialized; data store unavailable, returning empty state");
                    return new FeatureFlagsState(false);
                }
            }
            if (user == null || user.Key == null)
            {
                _evalLog.Warn("AllFlagsState() called with null user or null user key; returning empty state");
                return new FeatureFlagsState(false);
            }

            var builder = new FeatureFlagsStateBuilder(options);
            var clientSideOnly = FlagsStateOption.HasOption(options, FlagsStateOption.ClientSideOnly);
            var withReasons = FlagsStateOption.HasOption(options, FlagsStateOption.WithReasons);
            var detailsOnlyIfTracked = FlagsStateOption.HasOption(options, FlagsStateOption.DetailsOnlyForTrackedFlags);
            KeyedItems<ItemDescriptor> flags;
            try
            {
                flags = _dataStore.GetAll(DataModel.Features);
            }
            catch (Exception e)
            {
                LogHelpers.LogException(_log, "Exception while retrieving flags for AllFlagsState", e);
                return new FeatureFlagsState(false);
            }
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
                    LogHelpers.LogException(_evalLog,
                        string.Format("Exception caught for feature flag \"{0}\" when evaluating all flags", flag.Key),
                        e);
                    EvaluationReason reason = EvaluationReason.ErrorReason(EvaluationErrorKind.Exception);
                    builder.AddFlag(flag.Key, new EvaluationDetail<LdValue>(LdValue.Null, null, reason));
                }
            }
            return builder.Build();
        }

        private EvaluationDetail<T> Evaluate<T>(string featureKey, User user, LdValue defaultValue, LdValue.Converter<T> converter,
            bool checkType, EventFactory eventFactory)
        {
            T defaultValueOfType = converter.ToType(defaultValue);
            if (!Initialized)
            {
                if (_dataStore.Initialized())
                {
                    _evalLog.Warn("Flag evaluation before client initialized; using last known values from data store");
                }
                else
                {
                    _evalLog.Warn("Flag evaluation before client initialized; data store unavailable, returning default value");
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.ClientNotReady));
                }
            }

            FeatureFlag featureFlag = null;
            try
            {
                featureFlag = GetFlag(featureKey);
                if (featureFlag == null)
                {
                    _evalLog.Info("Unknown feature flag \"{0}\"; returning default value",
                        featureKey);
                    _eventProcessor.RecordEvaluationEvent(eventFactory.NewUnknownFlagEvaluationEvent(
                        featureKey, user, defaultValue, EvaluationErrorKind.FlagNotFound));
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.FlagNotFound));
                }

                if (user == null || user.Key == null)
                {
                    _evalLog.Warn("Null user or null user key when evaluating flag \"{0}\"; returning default value", featureKey);
                    _eventProcessor.RecordEvaluationEvent(eventFactory.NewDefaultValueEvaluationEvent(
                        featureFlag, user, defaultValue, EvaluationErrorKind.UserNotSpecified));
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.UserNotSpecified));
                }
                
                Evaluator.EvalResult evalResult = _evaluator.Evaluate(featureFlag, user, eventFactory);
                if (!IsOffline())
                {
                    foreach (var prereqEvent in evalResult.PrerequisiteEvents)
                    {
                        _eventProcessor.RecordEvaluationEvent(prereqEvent);
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
                        _evalLog.Error("Expected type {0} but got {1} when evaluating feature flag \"{2}\"; returning default value",
                            defaultValue.Type,
                            evalDetail.Value.Type,
                            featureKey);

                        _eventProcessor.RecordEvaluationEvent(eventFactory.NewDefaultValueEvaluationEvent(
                            featureFlag, user, defaultValue, EvaluationErrorKind.WrongType));
                        return new EvaluationDetail<T>(defaultValueOfType, null,
                            EvaluationReason.ErrorReason(EvaluationErrorKind.WrongType));
                    }
                    returnDetail = new EvaluationDetail<T>(converter.ToType(evalDetail.Value),
                        evalDetail.VariationIndex, evalDetail.Reason);
                }
                _eventProcessor.RecordEvaluationEvent(eventFactory.NewEvaluationEvent(
                    featureFlag, user, evalDetail, defaultValue));
                return returnDetail;
            }
            catch (Exception e)
            {
                LogHelpers.LogException(_evalLog,
                    string.Format("Exception when evaluating feature flag \"{0}\"", featureKey),
                    e);
                var reason = EvaluationReason.ErrorReason(EvaluationErrorKind.Exception);
                if (featureFlag == null)
                {
                    _eventProcessor.RecordEvaluationEvent(eventFactory.NewUnknownFlagEvaluationEvent(
                        featureKey, user, defaultValue, EvaluationErrorKind.Exception));
                }
                else
                {
                    _eventProcessor.RecordEvaluationEvent(eventFactory.NewEvaluationEvent(
                        featureFlag, user, new EvaluationDetail<LdValue>(defaultValue, null, reason), defaultValue));
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
        public void Track(string name, User user) =>
            TrackInternal(name, user, LdValue.Null, null);

        /// <inheritdoc/>
        public void Track(string name, User user, LdValue data) =>
            TrackInternal(name, user, data, null);

        /// <inheritdoc/>
        public void Track(string name, User user, LdValue data, double metricValue) =>
            TrackInternal(name, user, data, metricValue);

        private void TrackInternal(string key, User user, LdValue data, double? metricValue)
        {
            if (user == null || String.IsNullOrEmpty(user.Key))
            {
                _log.Warn("Track called with null user or null user key");
                return;
            }
            _eventProcessor.RecordCustomEvent(new EventProcessorTypes.CustomEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                User = user,
                EventKey = key,
                Data = data,
                MetricValue = metricValue
            });
        }

        /// <inheritdoc/>
        public void Identify(User user)
        {
            if (user == null || String.IsNullOrEmpty(user.Key))
            {
                _log.Warn("Identify called with null user or null user key");
                return;
            }
            _eventProcessor.RecordIdentifyEvent(new EventProcessorTypes.IdentifyEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                User = user
            });
        }

        /// <inheritdoc/>
        public void Alias(User currentUser, User previousUser)
        {
            if (currentUser == null || previousUser == null ||
                string.IsNullOrEmpty(currentUser.Key) || string.IsNullOrEmpty(previousUser.Key))
            {
                _log.Warn("Alias called with null user or null user key");
                return;
            }
            _eventProcessor.RecordAliasEvent(new EventProcessorTypes.AliasEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                CurrentKey = currentUser.Key,
                CurrentKind = currentUser.Anonymous ? EventProcessorTypes.ContextKind.AnonymousUser :
                    EventProcessorTypes.ContextKind.User,
                PreviousKey = previousUser.Key,
                PreviousKind = previousUser.Anonymous ? EventProcessorTypes.ContextKind.AnonymousUser :
                    EventProcessorTypes.ContextKind.User
            });
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
        /// (<see cref="ConfigurationBuilder.DataStore(IDataStoreFactory)"/>, etc.)
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

        #endregion

        #region Private methods

        private FeatureFlag GetFlag(string key)
        {
            var maybeItem = _dataStore.Get(DataModel.Features, key);
            if (maybeItem.HasValue && maybeItem.Value.Item != null && maybeItem.Value.Item is FeatureFlag f)
            {
                return f;
            }
            return null;
        }

        private Segment GetSegment(string key)
        {
            var maybeItem = _dataStore.Get(DataModel.Segments, key);
            if (maybeItem.HasValue && maybeItem.Value.Item != null && maybeItem.Value.Item is Segment s)
            {
                return s;
            }
            return null;
        }

        private void Dispose(bool disposing)
        {
            if (disposing) // follow standard IDisposable pattern
            {
                _log.Info("Closing LaunchDarkly client");
                _eventProcessor.Dispose();
                _dataStore.Dispose();
                _dataSource.Dispose();
                _bigSegmentStoreWrapper?.Dispose();
            }
        }

        #endregion
    }
}