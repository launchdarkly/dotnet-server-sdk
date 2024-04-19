using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.Sdk.Server.Subsystems;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <summary>
    /// Used to track information related to a migration operation.
    /// </summary>
    public sealed class MigrationOpTracker
    {
        #region Construction Data

        private readonly MigrationStage _stage;
        private readonly MigrationStage _defaultStage;
        private readonly string _flagKey;
        private readonly FeatureFlag _flag;
        private readonly Context _context;
        private readonly long _checkRatio;
        private readonly Logger _logger;
        private EvaluationDetail<string> _detail;

        private readonly object _lock = new object();

        #endregion

        #region Data Throughout Operation

        private MigrationOperation? _operation;
        private bool _oldError;
        private bool _newError;
        private bool _oldInvoked;
        private bool _newInvoked;
        private bool? _consistent;

        private TimeSpan? _oldLatency;
        private TimeSpan? _newLatency;

        #endregion

        internal MigrationOpTracker(
            MigrationStage stage,
            MigrationStage defaultStage,
            string flagKey,
            FeatureFlag flag,
            Context context,
            long checkRatio,
            Logger logger,
            EvaluationDetail<string> detail
        )
        {
            _stage = stage;
            _defaultStage = defaultStage;
            _flagKey = flagKey;
            _flag = flag;
            _context = context;
            _checkRatio = checkRatio;
            _logger = logger;
            _detail = detail;
        }

        #region Public Methods

        /// <summary>
        /// Sets the migration related operation associated with these tracking measurements.
        /// </summary>
        /// <param name="operation">the operation being tracked</param>
        public void Op(MigrationOperation operation)
        {
            lock (_lock)
            {
                _operation = operation;
            }
        }

        /// <summary>
        /// Report that an error has occurred for the specified origin.
        /// </summary>
        /// <param name="origin">the origin of the error</param>
        public void Error(MigrationOrigin origin)
        {
            lock (_lock)
            {
                switch (origin)
                {
                    case MigrationOrigin.Old:
                        this._oldError = true;
                        break;
                    case MigrationOrigin.New:
                        this._newError = true;
                        break;
                }
            }
        }

        /// <summary>
        /// Check the consistency of a read result. This method should be invoked if the `check` function
        /// is defined for the migration and both reads ("new"/"old") were done.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the consistency check function throws an exception, then no measurement for consistency will be included
        /// in the generated migration op event.
        /// </para>
        /// <para>
        /// Example calling the check function from the migration config.
        /// </para>
        /// <code>
        ///  if (checker != null &amp;&amp;
        ///   oldResult.IsSuccessful &amp;&amp;
        ///   newResult.IsSuccessful
        /// ) {
        ///   tracker.Consistency(() =&amp;gt; checker(oldResult.Result,
        ///   oldResult.Result));
        /// }
        /// </code>
        /// </remarks>
        /// <param name="checker">check method which returns true if the results were consistent</param>
        public void Consistency(Func<bool> checker)
        {
            if (!Sampler.Sample(_checkRatio)) return;

            lock (_lock)
            {
                try
                {
                    _consistent = checker();
                }
                catch (Exception e)
                {
                    _logger.Error("Exception executing migration comparison method: {0}", e);
                }
            }
        }

        /// <summary>
        /// Report the latency of an operation.
        /// </summary>
        /// <param name="origin">the origin the latency is being reported for</param>
        /// <param name="duration">the latency of the operation</param>
        public void Latency(MigrationOrigin origin, TimeSpan duration)
        {
            lock (_lock)
            {
                switch (origin)
                {
                    case MigrationOrigin.Old:
                        _oldLatency = duration;
                        break;
                    case MigrationOrigin.New:
                        _newLatency = duration;
                        break;
                }
            }
        }

        /// <summary>
        /// Call this to report that an origin was invoked (executed). There are some situations where the
        /// expectation is that both the old and new implementation will be used, but with writes
        /// it is possible that the non-authoritative will not execute. Reporting the execution allows
        /// for more accurate analytics.
        /// </summary>
        /// <param name="origin">the origin that was invoked</param>
        public void Invoked(MigrationOrigin origin)
        {
            lock (_lock)
            {
                switch (origin)
                {
                    case MigrationOrigin.Old:
                        _oldInvoked = true;
                        break;
                    case MigrationOrigin.New:
                        _newInvoked = true;
                        break;
                }
            }
        }

        #endregion

        private bool CheckOriginEventConsistency(MigrationOrigin origin)
        {
            if (origin == MigrationOrigin.Old ? _oldInvoked : _newInvoked)
            {
                return true;
            }

            // The origin was not invoked so any measurements involving it represent an inconsistency.
            var logTag = $"For migration op({_operation}) flagKey({_flagKey}):";

            if (origin == MigrationOrigin.Old ? _oldLatency.HasValue : _newLatency.HasValue)
            {
                _logger.Error($"{logTag} Latency was recorded for {origin}, but that origin was not invoked.");
                return false;
            }

            if (origin == MigrationOrigin.Old ? _oldError : _newError)
            {
                _logger.Error($"{logTag} Error reported for {origin}, but that origin was not invoked.");
                return false;
            }

            if (_consistent.HasValue)
            {
                _logger.Error($"{logTag} Consistency check was done, but {origin} was not invoked." +
                              " Both \"old\" and \"new\" must be invoked to do a comparison.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check for inconsistencies in the data used to generate an event and log any inconsistencies.
        /// </summary>
        /// <returns>true if the data is consistent</returns>
        private bool CheckEventConsistency()
        {
            return CheckOriginEventConsistency(MigrationOrigin.Old) &&
            CheckOriginEventConsistency(MigrationOrigin.New);
        }

        internal EventProcessorTypes.MigrationOpEvent? CreateEvent()
        {
            lock (_lock)
            {
                if (_flagKey.Length == 0)
                {
                    _logger.Error("The migration was executed against an empty flag key and no event will be created.");
                    return null;
                }
                if (!_operation.HasValue)
                {
                    _logger.Error("The operation must be set, using \"op\" before an event can be created.");
                    return null;
                }

                if (!_oldInvoked && !_newInvoked)
                {
                    _logger.Error("The migration invoked neither the \"old\" or \"new\" implementation and an" +
                                  " event cannot be generated.");
                    return null;
                }

                if (!_context.Valid)
                {
                    _logger.Error("The migration was not done against a valid context and cannot generate an event.");
                    return null;
                }

                if (!CheckEventConsistency())
                {
                    return null;
                }

                var migrationOpEvent = new EventProcessorTypes.MigrationOpEvent
                {
                    Timestamp = UnixMillisecondTime.Now,
                    Context = _context,
                    Operation = _operation.Value.ToDataModelString(),
                    SamplingRatio = _flag?.SamplingRatio ?? 1,
                    FlagKey = _flagKey,
                    FlagVersion = _flag?.Version,
                    Variation = _detail.VariationIndex,
                    Value = LdValue.Of(_stage.ToDataModelString()),
                    Default = LdValue.Of(_defaultStage.ToDataModelString()),
                    Reason = _detail.Reason,
                    Invoked = new EventProcessorTypes.MigrationOpEvent.InvokedMeasurement
                    {
                        Old = _oldInvoked,
                        New = _newInvoked
                    }
                };

                if (_oldLatency.HasValue || _newLatency.HasValue)
                {
                    migrationOpEvent.Latency = new EventProcessorTypes.MigrationOpEvent.LatencyMeasurement
                    {
                        Old = _oldLatency?.Milliseconds,
                        New = _newLatency?.Milliseconds
                    };
                }

                if (_oldError || _newError)
                {
                    migrationOpEvent.Error = new EventProcessorTypes.MigrationOpEvent.ErrorMeasurement
                    {
                        Old = _oldError,
                        New = _newError
                    };
                }

                if (_consistent.HasValue)
                {
                    migrationOpEvent.Consistent = new EventProcessorTypes.MigrationOpEvent.ConsistentMeasurement
                    {
                        IsConsistent = _consistent.Value,
                        SamplingRatio = _checkRatio
                    };
                }

                return migrationOpEvent;
            }
        }
    }
}
