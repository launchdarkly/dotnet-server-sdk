﻿using System;
using System.Linq;
using System.Security.Cryptography;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Server.Hooks;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.BigSegments;
using LaunchDarkly.Sdk.Server.Internal.DataSources;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Evaluation;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Hooks.Executor;
using LaunchDarkly.Sdk.Server.Internal.Hooks.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.Sdk.Server.Migrations;
using LaunchDarkly.Sdk.Server.Subsystems;

using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;

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
        private readonly IHookExecutor _hookExecutor;

        private readonly TimeSpan ExcessiveInitWaitTime = TimeSpan.FromSeconds(60);
        private const String InitWaitTimeInfo = "Waiting up to {0} milliseconds for LaunchDarkly client to start.";
        private const String ExcessiveInitWaitTimeWarning =
            "LaunchDarkly client created with StartWaitTime of {0} milliseconds.  We recommend a timeout of less than {1} milliseconds.";
        private const String DidNotInitializeTimelyWarning = "Client did not initialize within {0} milliseconds.";

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
        /// <seealso cref="LdClient(string)"/>
        public LdClient(Configuration config)
        {
            _configuration = config;

            var logConfig = (config.Logging ?? Components.Logging()).Build(new LdClientContext(config.SdkKey));

            _log = logConfig.LogAdapter.Logger(logConfig.BaseLoggerName ?? LogNames.DefaultBase);
            _log.Info("Starting LaunchDarkly client {0}",
                AssemblyVersions.GetAssemblyVersionStringForType(typeof(LdClient)));
            _evalLog = _log.SubLogger(LogNames.EvaluationSubLog);

            var taskExecutor = new TaskExecutor(this, _log);

            var clientContext = new LdClientContext(
                config.SdkKey,
                null,
                null,
                null,
                _log,
                config.Offline,
                config.ServiceEndpoints,
                null,
                taskExecutor,
                config.ApplicationInfo?.Build() ?? new ApplicationInfo(),
                config.WrapperInfo?.Build()
                );

            var httpConfig = (config.Http ?? Components.HttpConfiguration()).Build(clientContext);
            clientContext = clientContext.WithHttp(httpConfig);

            var diagnosticStore = _configuration.DiagnosticOptOut ? null :
                new ServerDiagnosticStore(config, clientContext);
            clientContext = clientContext.WithDiagnosticStore(diagnosticStore);

            var dataStoreUpdates = new DataStoreUpdatesImpl(taskExecutor, _log.SubLogger(LogNames.DataStoreSubLog));

            _dataStore = (_configuration.DataStore ?? Components.InMemoryDataStore)
                .Build(clientContext.WithDataStoreUpdates(dataStoreUpdates));
            _dataStoreStatusProvider = new DataStoreStatusProviderImpl(_dataStore, dataStoreUpdates);

            var bigSegmentsConfig = (_configuration.BigSegments ?? Components.BigSegments(null))
                .Build(clientContext);
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
                    _bigSegmentStoreWrapper.GetMembership,
                _log
                );

            var eventProcessorFactory =
                config.Offline ? Components.NoEvents :
                (_configuration.Events?? Components.SendEvents());
            _eventProcessor = eventProcessorFactory.Build(clientContext);

            var dataSourceUpdates = new DataSourceUpdatesImpl(_dataStore, _dataStoreStatusProvider,
                taskExecutor, _log, logConfig.LogDataSourceOutageAsErrorAfter);
            IComponentConfigurer<IDataSource> dataSourceFactory =
                config.Offline ? Components.ExternalUpdatesOnly :
                (_configuration.DataSource ?? Components.StreamingDataSource());
            _dataSource = dataSourceFactory.Build(clientContext.WithDataSourceUpdates(dataSourceUpdates));
            _dataSourceStatusProvider = new DataSourceStatusProviderImpl(dataSourceUpdates);
            _flagTracker = new FlagTrackerImpl(dataSourceUpdates,
                (string key, Context context) => JsonVariation(key, context, LdValue.Null));

            var hookConfig = (config.Hooks ?? Components.Hooks()).Build();
            _hookExecutor =  hookConfig.Hooks.Any() ?
                (IHookExecutor) new Executor(_log.SubLogger(LogNames.HooksSubLog), hookConfig.Hooks)
                : new NoopExecutor();


            var initTask = _dataSource.Start();

            if (!(_dataSource is ComponentsImpl.NullDataSource))
            {
                _log.Info(InitWaitTimeInfo, _configuration.StartWaitTime.TotalMilliseconds);
                if (_configuration.StartWaitTime >= ExcessiveInitWaitTime)
                {
                    _log.Warn(ExcessiveInitWaitTimeWarning, _configuration.StartWaitTime.TotalMilliseconds, ExcessiveInitWaitTime.TotalMilliseconds);
                }
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
        /// If you need to specify any custom SDK options, use <see cref="LdClient(Configuration)"/>
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
        /// <see cref="LdClient(Configuration)"/>.
        /// </para>
        /// </remarks>
        /// <param name="sdkKey">the SDK key for your LaunchDarkly environment</param>
        /// <seealso cref="LdClient(Configuration)"/>
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
        public bool BoolVariation(string key, Context context, bool defaultValue = false) =>
            Evaluate(Method.BoolVariation, key, context, LdValue.Of(defaultValue), LdValue.Convert.Bool, true, EventFactory.Default).Value;

        /// <inheritdoc/>
        public int IntVariation(string key, Context context, int defaultValue) =>
            Evaluate(Method.IntVariation, key, context, LdValue.Of(defaultValue), LdValue.Convert.Int, true, EventFactory.Default).Value;

        /// <inheritdoc/>
        public float FloatVariation(string key, Context context, float defaultValue) =>
            Evaluate(Method.FloatVariation, key, context, LdValue.Of(defaultValue), LdValue.Convert.Float, true, EventFactory.Default).Value;

        /// <inheritdoc/>
        public double DoubleVariation(string key, Context context, double defaultValue) =>
            Evaluate(Method.DoubleVariation, key, context, LdValue.Of(defaultValue), LdValue.Convert.Double, true, EventFactory.Default).Value;

        /// <inheritdoc/>
        public string StringVariation(string key, Context context, string defaultValue) =>
            Evaluate(Method.StringVariation, key, context, LdValue.Of(defaultValue), LdValue.Convert.String, true, EventFactory.Default).Value;

        /// <inheritdoc/>
        public LdValue JsonVariation(string key, Context context, LdValue defaultValue) =>
            Evaluate(Method.JsonVariation, key, context, defaultValue, LdValue.Convert.Json, false, EventFactory.Default).Value;

        /// <inheritdoc/>
        public EvaluationDetail<bool> BoolVariationDetail(string key, Context context, bool defaultValue) =>
            Evaluate(Method.BoolVariationDetail, key, context, LdValue.Of(defaultValue), LdValue.Convert.Bool, true, EventFactory.DefaultWithReasons);

        /// <inheritdoc/>
        public EvaluationDetail<int> IntVariationDetail(string key, Context context, int defaultValue) =>
            Evaluate(Method.IntVariationDetail, key, context, LdValue.Of(defaultValue), LdValue.Convert.Int, true, EventFactory.DefaultWithReasons);

        /// <inheritdoc/>
        public EvaluationDetail<float> FloatVariationDetail(string key, Context context, float defaultValue) =>
            Evaluate(Method.FloatVariationDetail, key, context, LdValue.Of(defaultValue), LdValue.Convert.Float, true, EventFactory.DefaultWithReasons);

        /// <inheritdoc/>
        public EvaluationDetail<double> DoubleVariationDetail(string key, Context context, double defaultValue) =>
            Evaluate(Method.DoubleVariationDetail, key, context, LdValue.Of(defaultValue), LdValue.Convert.Double, true, EventFactory.DefaultWithReasons);

        /// <inheritdoc/>
        public EvaluationDetail<string> StringVariationDetail(string key, Context context, string defaultValue) =>
            Evaluate(Method.StringVariationDetail, key, context, LdValue.Of(defaultValue), LdValue.Convert.String, true, EventFactory.DefaultWithReasons);

        /// <inheritdoc/>
        public EvaluationDetail<LdValue> JsonVariationDetail(string key, Context context, LdValue defaultValue) =>
            Evaluate(Method.JsonVariationDetail, key, context, defaultValue, LdValue.Convert.Json, false, EventFactory.DefaultWithReasons);

        /// <inheritdoc/>
        public MigrationVariation MigrationVariation(string key, Context context, MigrationStage defaultStage)
        {

            var (detail, flag) = EvaluateWithHooks(Method.MigrationVariation, key, context, LdValue.Of(defaultStage.ToDataModelString()),
                              LdValue.Convert.String, true, EventFactory.Default);

            var nullableStage  = MigrationStageExtensions.FromDataModelString(detail.Value);
            var stage = nullableStage ?? defaultStage;
            if (nullableStage == null)
            {
                _log.Error($"Unrecognized MigrationStage for {key}; using default stage.");
                detail = new EvaluationDetail<string>(defaultStage.ToDataModelString(), null,
                    EvaluationReason.ErrorReason(EvaluationErrorKind.WrongType));
            }

            return new MigrationVariation(stage, new MigrationOpTracker(
                stage,
                defaultStage,
                key,
                flag,
                context,
                flag?.Migration?.CheckRatio ?? 1,
                _log,
                detail
                ));
        }

        /// <inheritdoc/>
        public FeatureFlagsState AllFlagsState(Context context, params FlagsStateOption[] options)
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
            if (!context.Valid)
            {
                _evalLog.Warn("AllFlagsState() called with invalid context ({0}); returning empty state", context.Error);
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
                    EvaluatorTypes.EvalResult result = _evaluator.Evaluate(flag, context);
                    bool inExperiment = EventFactory.IsExperiment(flag, result.Result.Reason);
                    builder.AddFlag(
                        flag.Key,
                        result.Result.Value,
                        result.Result.VariationIndex,
                        result.Result.Reason,
                        flag.Version,
                        flag.TrackEvents || inExperiment,
                        inExperiment,
                        flag.DebugEventsUntilDate
                        );
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

        private (EvaluationDetail<T>, FeatureFlag) EvaluateWithHooks<T>(string method, string key, Context context, LdValue defaultValue, LdValue.Converter<T> converter,
            bool checkType, EventFactory eventFactory)
        {
            var evalSeriesContext = new EvaluationSeriesContext(key, context, defaultValue, method);
            return _hookExecutor.EvaluationSeries(
                evalSeriesContext,
                converter,
                () => EvaluationAndFlag(key, context, defaultValue, converter, checkType, eventFactory));
        }

        private (EvaluationDetail<T>, FeatureFlag) EvaluationAndFlag<T>(string featureKey, Context context,
            LdValue defaultValue, LdValue.Converter<T> converter,
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
                    return (new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.ClientNotReady)), null);
                }
            }

            if (!context.Valid)
            {
                _evalLog.Warn("Invalid evaluation context when evaluating flag \"{0}\" ({1}); returning default value", featureKey,
                    context.Error);
                return (new EvaluationDetail<T>(defaultValueOfType, null,
                    EvaluationReason.ErrorReason(EvaluationErrorKind.UserNotSpecified)), null);
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
                        featureKey, context, defaultValue, EvaluationErrorKind.FlagNotFound));
                    return (new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.FlagNotFound)), null);
                }

                EvaluatorTypes.EvalResult evalResult = _evaluator.Evaluate(featureFlag, context);
                if (!IsOffline())
                {
                    foreach (var prereqEvent in evalResult.PrerequisiteEvals)
                    {
                        _eventProcessor.RecordEvaluationEvent(eventFactory.NewPrerequisiteEvaluationEvent(
                            prereqEvent.PrerequisiteFlag, context, prereqEvent.Result, prereqEvent.PrerequisiteOfFlagKey));
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
                            featureFlag, context, defaultValue, EvaluationErrorKind.WrongType));
                        return (new EvaluationDetail<T>(defaultValueOfType, null,
                            EvaluationReason.ErrorReason(EvaluationErrorKind.WrongType)), featureFlag);
                    }
                    returnDetail = new EvaluationDetail<T>(converter.ToType(evalDetail.Value),
                        evalDetail.VariationIndex, evalDetail.Reason);
                }
                _eventProcessor.RecordEvaluationEvent(eventFactory.NewEvaluationEvent(
                    featureFlag, context, evalDetail, defaultValue));
                return (returnDetail, featureFlag);
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
                        featureKey, context, defaultValue, EvaluationErrorKind.Exception));
                }
                else
                {
                    _eventProcessor.RecordEvaluationEvent(eventFactory.NewEvaluationEvent(
                        featureFlag, context, new EvaluationDetail<LdValue>(defaultValue, null, reason), defaultValue));
                }
                return (new EvaluationDetail<T>(defaultValueOfType, null, reason), null);
            }
        }

        private EvaluationDetail<T> Evaluate<T>(string method, string featureKey, Context context, LdValue defaultValue, LdValue.Converter<T> converter,
            bool checkType, EventFactory eventFactory)
        {
            return EvaluateWithHooks(method, featureKey, context, defaultValue, converter, checkType, eventFactory).Item1;
        }

        /// <inheritdoc/>
        public string SecureModeHash(Context context)
        {
            if (!context.Valid)
            {
                return null;
            }
            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            byte[] keyBytes = encoding.GetBytes(_configuration.SdkKey);

            HMACSHA256 hmacSha256 = new HMACSHA256(keyBytes);
            byte[] hashedMessage = hmacSha256.ComputeHash(encoding.GetBytes(context.FullyQualifiedKey));
            return BitConverter.ToString(hashedMessage).Replace("-", "").ToLower();
        }

        /// <inheritdoc/>
        public void Track(string name, Context context) =>
            TrackInternal(name, context, LdValue.Null, null);

        /// <inheritdoc/>
        public void Track(string name, Context context, LdValue data) =>
            TrackInternal(name, context, data, null);

        /// <inheritdoc/>
        public void Track(string name, Context context, LdValue data, double metricValue) =>
            TrackInternal(name, context, data, metricValue);

        /// <inheritdoc/>
        public void TrackMigration(MigrationOpTracker tracker)
        {
            var optEvent = tracker.CreateEvent();
            // An event could not be created. The tracker will log the relevant error details.
            if (!optEvent.HasValue) return;

            _eventProcessor.RecordMigrationEvent(optEvent.Value);
        }

        private void TrackInternal(string key, Context context, LdValue data, double? metricValue)
        {
            if (!context.Valid)
            {
                _log.Warn("Track called with invalid context ({0})", context.Error);
                return;
            }
            _eventProcessor.RecordCustomEvent(new EventProcessorTypes.CustomEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                Context = context,
                EventKey = key,
                Data = data,
                MetricValue = metricValue
            });
        }

        /// <inheritdoc/>
        public void Identify(Context context)
        {
            if (!context.Valid)
            {
                _log.Warn("Identify called with invalid context ({0})", context.Error);
                return;
            }
            _eventProcessor.RecordIdentifyEvent(new EventProcessorTypes.IdentifyEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                Context = context
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
        /// (<see cref="ConfigurationBuilder.DataStore"/>, etc.)
        /// will also be disposed of by this method; their lifecycle is the same as the client's.
        /// </para>
        /// </remarks>
        /// <see cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public void Flush() =>
            _eventProcessor.Flush();

        /// <inheritdoc/>
        public bool FlushAndWait(TimeSpan timeout) =>
            _eventProcessor.FlushAndWait(timeout);

        /// <inheritdoc/>
        public Logger GetLogger()
        {
            return _log;
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
                _hookExecutor.Dispose();
                _eventProcessor.Dispose();
                _dataStore.Dispose();
                _dataSource.Dispose();
                _bigSegmentStoreWrapper?.Dispose();
            }
        }

        #endregion
    }
}
