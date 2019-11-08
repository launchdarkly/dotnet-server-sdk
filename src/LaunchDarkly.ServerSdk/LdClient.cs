using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Common.Logging;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
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
        private readonly IFeatureStore _featureStore;
        internal readonly IUpdateProcessor _updateProcessor;
        private bool _shouldDisposeEventProcessor;
        private bool _shouldDisposeFeatureStore;

        /// <summary>
        /// Deprecated; please use <see cref="IConfigurationBuilder.EventProcessorFactory(IEventProcessorFactory)"/>
        /// instead if you want to specify a custom analytics event processor.
        /// </summary>
        /// <param name="config">a client configuration object</param>
        /// <param name="eventProcessor">an event processor</param>
        [Obsolete("Deprecated, please use Configuration.WithEventProcessorFactory")]
        public LdClient(Configuration config, IEventProcessor eventProcessor)
        {
            Log.InfoFormat("Starting LaunchDarkly Client {0}",
                ServerSideClientEnvironment.Instance.Version);

            _configuration = config;

            if (eventProcessor == null)
            {
                _eventProcessor = (_configuration.EventProcessorFactory ??
                    Components.DefaultEventProcessor).CreateEventProcessor(_configuration);
                _shouldDisposeEventProcessor = true;
            }
            else
            {
                _eventProcessor = eventProcessor;
                // The following line is for backward compatibility with the obsolete mechanism by which the
                // caller could pass in an IStoreEvents implementation instance that we did not create.  We
                // were not disposing of that instance when the client was closed, so we should continue not
                // doing so until the next major version eliminates that mechanism.  We will always dispose
                // of instances that we created ourselves from a factory.
                _shouldDisposeEventProcessor = false;
            }

            IFeatureStore store;
            if (_configuration.FeatureStore == null)
            {
                store = (_configuration.FeatureStoreFactory ??
                    Components.InMemoryFeatureStore).CreateFeatureStore();
                _shouldDisposeFeatureStore = true;
            }
            else
            {
                store = _configuration.FeatureStore;
                _shouldDisposeFeatureStore = false; // see previous comment
            }
            _featureStore = new FeatureStoreClientWrapper(store);

            _updateProcessor = (_configuration.UpdateProcessorFactory ??
                Components.DefaultUpdateProcessor).CreateUpdateProcessor(_configuration, _featureStore);

            var initTask = _updateProcessor.Start();

            if (!(_updateProcessor is NullUpdateProcessor))
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
#pragma warning disable 618  // suppress warning for calling obsolete ctor
        public LdClient(Configuration config) : this(config, null)
        #pragma warning restore 618
        {
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
            return IsOffline() || _updateProcessor.Initialized();
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
        [Obsolete("Use the ImmutableJsonValue-based overload of JsonVariation")]
        public JToken JsonVariation(string key, User user, JToken defaultValue)
        {
            return Evaluate(key, user, LdValue.FromSafeValue(defaultValue), LdValue.Convert.UnsafeJToken, false, EventFactory.Default).Value;
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
        [Obsolete("Use the ImmutableJsonValue-based overload of JsonVariation")]
        public EvaluationDetail<JToken> JsonVariationDetail(string key, User user, JToken defaultValue)
        {
            return Evaluate(key, user, LdValue.FromSafeValue(defaultValue), LdValue.Convert.UnsafeJToken, false, EventFactory.DefaultWithReasons);
        }

        /// <inheritdoc/>
        public EvaluationDetail<LdValue> JsonVariationDetail(string key, User user, LdValue defaultValue)
        {
            return Evaluate(key, user, defaultValue, LdValue.Convert.Json, false, EventFactory.DefaultWithReasons);
        }

        /// <inheritdoc/>
        [Obsolete("Use AllFlagsState instead. Current versions of the client-side SDK will not generate analytics events correctly if you pass the result of AllFlags.")]
        public IDictionary<string, JToken> AllFlags(User user)
        {
            var state = AllFlagsState(user);
            if (!state.Valid)
            {
                return null;
            }
            return state.ToValuesMap();
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
                if (_featureStore.Initialized())
                {
                    Log.Warn("AllFlagsState() called before client initialized; using last known values from feature store");
                }
                else
                {
                    Log.Warn("AllFlagsState() called before client initialized; feature store unavailable, returning empty state");
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
            IDictionary<string, FeatureFlag> flags = _featureStore.All(VersionedDataKind.Features);
            foreach (KeyValuePair<string, FeatureFlag> pair in flags)
            {
                var flag = pair.Value;
                if (clientSideOnly && !flag.ClientSide)
                {
                    continue;
                }
                try
                {
                    FeatureFlag.EvalResult result = flag.Evaluate(user, _featureStore, EventFactory.Default);
                    state.AddFlag(flag, result.Result.Value.InnerValue, result.Result.VariationIndex,
                        withReasons ? result.Result.Reason : null, detailsOnlyIfTracked);
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("Exception caught for feature flag \"{0}\" when evaluating all flags: {1}", flag.Key, Util.ExceptionMessage(e));
                    Log.Debug(e.ToString(), e);
                    EvaluationReason reason = EvaluationReason.ErrorReason(EvaluationErrorKind.EXCEPTION);
                    state.AddFlag(flag, null, null, withReasons ? reason : null, detailsOnlyIfTracked);
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
                if (_featureStore.Initialized())
                {
                    Log.Warn("Flag evaluation before client initialized; using last known values from feature store");
                }
                else
                {
                    Log.Warn("Flag evaluation before client initialized; feature store unavailable, returning default value");
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.CLIENT_NOT_READY));
                }
            }

            FeatureFlag featureFlag = null;
            try
            {
                featureFlag = _featureStore.Get(VersionedDataKind.Features, featureKey);
                if (featureFlag == null)
                {
                    Log.InfoFormat("Unknown feature flag {0}; returning default value",
                        featureKey);
                    _eventProcessor.SendEvent(eventFactory.NewUnknownFeatureRequestEvent(featureKey, user, defaultValue,
                        EvaluationErrorKind.FLAG_NOT_FOUND));
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.FLAG_NOT_FOUND));
                }

                if (user == null || user.Key == null)
                {
                    Log.Warn("Feature flag evaluation called with null user or null user key. Returning default");
                    _eventProcessor.SendEvent(eventFactory.NewDefaultFeatureRequestEvent(featureFlag, user, defaultValue,
                        EvaluationErrorKind.USER_NOT_SPECIFIED));
                    return new EvaluationDetail<T>(defaultValueOfType, null,
                        EvaluationReason.ErrorReason(EvaluationErrorKind.USER_NOT_SPECIFIED));
                }
                
                FeatureFlag.EvalResult evalResult = featureFlag.Evaluate(user, _featureStore, eventFactory);
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

                        _eventProcessor.SendEvent(eventFactory.NewDefaultFeatureRequestEvent(featureFlag, user,
                            defaultValue, EvaluationErrorKind.WRONG_TYPE));
                        return new EvaluationDetail<T>(defaultValueOfType, null,
                            EvaluationReason.ErrorReason(EvaluationErrorKind.WRONG_TYPE));
                    }
                    returnDetail = new EvaluationDetail<T>(converter.ToType(evalDetail.Value),
                        evalDetail.VariationIndex, evalDetail.Reason);
                }
                _eventProcessor.SendEvent(eventFactory.NewFeatureRequestEvent(featureFlag, user,
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
                    _eventProcessor.SendEvent(eventFactory.NewFeatureRequestEvent(featureFlag, user,
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
        [Obsolete("Use Track(string, User, ImmutableJsonValue")]
        public void Track(string name, User user, string data)
        {
            Track(name, user, LdValue.Of(data));
        }

        /// <inheritdoc/>
        [Obsolete("Use Track(string, User, ImmutableJsonValue")]
        public void Track(string name, JToken data, User user)
        {
            Track(name, user, LdValue.FromSafeValue(data));
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
                // See comments in LdClient constructor: eventually all of these implementation objects
                // will be factory-created and will have the same lifecycle as the client.
                if (_shouldDisposeEventProcessor)
                {
                    _eventProcessor.Dispose();
                }
                if (_shouldDisposeFeatureStore)
                {
                    _featureStore.Dispose();
                }
                _updateProcessor.Dispose();
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
        /// (<see cref="ConfigurationExtensions.WithFeatureStore(Configuration, IFeatureStore)"/>, etc.)
        /// will also be disposed of by this method; their lifecycle is the same as the client's.
        /// However, for any components that you constructed yourself and passed in (via the deprecated
        /// method <see cref="ConfigurationExtensions.WithFeatureStore(Configuration, IFeatureStore)"/>,
        /// or the deprecated <c>LdClient</c> constructor that takes an <see cref="IEventProcessor"/>),
        /// this will not happen; you are responsible for managing their lifecycle.
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