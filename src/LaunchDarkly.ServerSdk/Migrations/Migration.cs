using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    /// <inheritdoc />
    internal class
        Migration<TReadResult, TWriteResult, TReadInput, TWriteInput> : IMigration<TReadResult, TWriteResult, TReadInput
            , TWriteInput>
        where TReadResult : class
        where TWriteResult : class
    {
        private readonly ILdClient _client;
        private readonly bool _trackLatency;
        private readonly bool _trackErrors;
        private readonly MigrationExecution _execution;

        private readonly Func<TReadInput, MigrationMethod.Result<TReadResult>> _readOld;
        private readonly Func<TReadInput, MigrationMethod.Result<TReadResult>> _readNew;

        private readonly Func<TWriteInput, MigrationMethod.Result<TWriteResult>> _writeOld;
        private readonly Func<TWriteInput, MigrationMethod.Result<TWriteResult>> _writeNew;

        private readonly Func<TReadResult, TReadResult, bool> _check;

        public Migration(
            ILdClient client,
            bool trackLatency,
            bool trackErrors,
            MigrationExecution execution,
            Func<TReadInput, MigrationMethod.Result<TReadResult>> readOld,
            Func<TReadInput, MigrationMethod.Result<TReadResult>> readNew,
            Func<TWriteInput, MigrationMethod.Result<TWriteResult>> writeOld,
            Func<TWriteInput, MigrationMethod.Result<TWriteResult>> writeNew,
            Func<TReadResult, TReadResult, bool> check
        )
        {
            _client = client;
            _trackLatency = trackLatency;
            _trackErrors = trackErrors;
            _execution = execution;
            _readOld = readOld;
            _readNew = readNew;
            _writeOld = writeOld;
            _writeNew = writeNew;
            _check = check;
        }

        #region Private Implementation

        private struct MultiReadResult
        {
            internal MigrationResult<TReadResult> Old { get; }
            internal MigrationResult<TReadResult> New { get; }

            public MultiReadResult(
                MigrationResult<TReadResult> resA,
                MigrationResult<TReadResult> resB)
            {
                if (resA.Origin == resB.Origin)
                {
                    // This exception is indicative of a bug in implementation.
                    throw new ArgumentException(
                        "The two results for a multi-read result must be from different origins.");
                }
                if (resA.Origin == MigrationOrigin.Old)
                {
                    Old = resA;
                    New = resB;
                }
                else
                {
                    New = resA;
                    Old = resB;
                }
            }
        }

        private static MigrationResult<TOpResult> SafeCall<TOpPayload, TOpResult>(
            TOpPayload payload,
            MigrationOrigin origin,
            Func<TOpPayload, MigrationMethod.Result<TOpResult>> method) where TOpResult : class
        {
            try
            {
                return method(payload).MigrationResult(origin);
            }
            catch (Exception e)
            {
                return MigrationMethod.Failure<TOpResult>(e).MigrationResult(origin);
            }
        }

        private MigrationResult<TOpResult> TrackLatency<TOpPayload, TOpResult>(
            TOpPayload payload,
            MigrationOpTracker tracker,
            MigrationOrigin origin,
            Func<TOpPayload, MigrationMethod.Result<TOpResult>> method) where TOpResult : class
        {
            if (!_trackLatency) return SafeCall(payload, origin, method);

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var result = SafeCall(payload, origin, method);
            stopWatch.Stop();
            tracker.Latency(origin, stopWatch.Elapsed);
            return result;
        }

        private MigrationResult<TOpResult> DoSingleOp<TOpPayload, TOpResult>(
            TOpPayload payload,
            MigrationOpTracker tracker,
            MigrationOrigin origin,
            Func<TOpPayload, MigrationMethod.Result<TOpResult>> method) where TOpResult : class
        {
            tracker.Invoked(origin);
            var res = TrackLatency(payload, tracker, origin, method);
            if (!res.IsSuccessful && _trackErrors)
            {
                tracker.Error(origin);
            }

            return res;
        }

        private MultiReadResult DoSerialRead(TReadInput payload, MigrationOpTracker tracker)
        {
            var flip = false;
            if (_execution.Order == MigrationSerialOrder.Random)
            {
                flip = Sampler.Sample(2);
            }

            if (flip)
            {
                var newResult = DoSingleOp(payload, tracker, MigrationOrigin.New, _readNew);
                var oldResult = DoSingleOp(payload, tracker, MigrationOrigin.Old, _readOld);
                return new MultiReadResult(oldResult, newResult);
            }
            else
            {
                var oldResult = DoSingleOp(payload, tracker, MigrationOrigin.Old, _readOld);
                var newResult = DoSingleOp(payload, tracker, MigrationOrigin.New, _readNew);
                return new MultiReadResult(oldResult, newResult);
            }
        }

        private MultiReadResult DoParallelRead(TReadInput payload, MigrationOpTracker tracker)
        {
            var tasks = new List<Func<TReadInput, MigrationResult<TReadResult>>>
            {
                (input) => DoSingleOp(payload, tracker, MigrationOrigin.Old, _readOld),
                (input) => DoSingleOp(payload, tracker, MigrationOrigin.New, _readNew),
            };
            var results = tasks.AsParallel().Select(
                task => task(payload)).ToArray();
            return new MultiReadResult(results.First(), results.Last());
        }

        private MultiReadResult DoMultiRead(TReadInput payload,
            MigrationOpTracker tracker)
        {
            MultiReadResult result;
            switch (_execution.Mode)
            {
                case MigrationExecutionMode.Serial:
                    result = DoSerialRead(payload, tracker);
                    break;
                case MigrationExecutionMode.Parallel:
                    result = DoParallelRead(payload, tracker);
                    break;
                default:
                    _client.GetLogger().Error("Unrecognized execution method, using serial execution for read.");
                    result = DoParallelRead(payload, tracker);
                    break;
            }

            if (_check != null && result.Old.IsSuccessful && result.New.IsSuccessful)
            {
                tracker.Consistency(() => _check(result.Old.Value, result.New.Value));
            }

            return result;
        }

        private MigrationResult<TReadResult> HandleRead(MigrationStage stage, MigrationOpTracker tracker,
            TReadInput payload)
        {
            switch (stage)
            {
                case MigrationStage.Off: // Intentional fallthrough
                case MigrationStage.DualWrite:
                    return DoSingleOp(payload, tracker, MigrationOrigin.Old, _readOld);
                case MigrationStage.Shadow:
                    return DoMultiRead(payload, tracker).Old;
                case MigrationStage.Live:
                    return DoMultiRead(payload, tracker).New;
                case MigrationStage.RampDown: // Intentional fallthrough
                case MigrationStage.Complete:
                    return DoSingleOp(payload, tracker, MigrationOrigin.New, _readNew);
                default:
                    // If this error occurs it would be because an additional migration stage
                    // was added, but this code was not updated to support it.
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        private MigrationWriteResult<TWriteResult> HandleWrite(MigrationStage stage, MigrationOpTracker tracker,
            TWriteInput payload)
        {
            switch (stage)
            {
                case MigrationStage.Off:
                    return new MigrationWriteResult<TWriteResult>(DoSingleOp(payload, tracker,
                        MigrationOrigin.Old, _writeOld));
                case MigrationStage.DualWrite: // Intentionally falls through.
                case MigrationStage.Shadow:
                {
                    var oldResult = DoSingleOp(payload, tracker, MigrationOrigin.Old, _writeOld);

                    if (!oldResult.IsSuccessful)
                    {
                        return new MigrationWriteResult<TWriteResult>(oldResult);
                    }

                    var newResult = DoSingleOp(payload, tracker, MigrationOrigin.New, _writeNew);
                    return new MigrationWriteResult<TWriteResult>(oldResult, newResult);
                }
                case MigrationStage.Live: // Intentionally falls through.
                case MigrationStage.RampDown:
                {
                    var newResult = DoSingleOp(payload, tracker, MigrationOrigin.New, _writeNew);

                    if (!newResult.IsSuccessful)
                    {
                        return new MigrationWriteResult<TWriteResult>(newResult);
                    }

                    var oldResult = DoSingleOp(payload, tracker, MigrationOrigin.Old, _writeOld);
                    return new MigrationWriteResult<TWriteResult>(newResult, oldResult);
                }
                case MigrationStage.Complete:
                    return new MigrationWriteResult<TWriteResult>(DoSingleOp(payload, tracker,
                        MigrationOrigin.New, _writeNew));
                default:
                {
                    // If this error occurs it would be because an additional migration stage
                    // was added, but this code was not updated to support it.
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
                }
            }
        }

        #endregion

        #region IMigration Implementation

        /// <inheritdoc />
        public MigrationResult<TReadResult> Read(string flagKey, Context context, MigrationStage defaultStage,
            TReadInput payload)
        {
            var (stage, tracker) = _client.MigrationVariation(flagKey, context, defaultStage);
            tracker.Op(MigrationOperation.Read);

            var result = HandleRead(stage, tracker, payload);

            _client.TrackMigration(tracker);

            return result;
        }

        /// <inheritdoc />
        public MigrationWriteResult<TWriteResult> Write(string flagKey, Context context, MigrationStage defaultStage,
            TWriteInput payload)
        {
            var (stage, tracker) = _client.MigrationVariation(flagKey, context, defaultStage);
            tracker.Op(MigrationOperation.Write);

            var result = HandleWrite(stage, tracker, payload);

            _client.TrackMigration(tracker);

            return result;
        }

        /// <inheritdoc />
        public MigrationResult<TReadResult> Read(string flagKey, Context context, MigrationStage defaultStage)
        {
            return Read(flagKey, context, defaultStage, default);
        }

        /// <inheritdoc />
        public MigrationWriteResult<TWriteResult> Write(string flagKey, Context context, MigrationStage defaultStage)
        {
            return Write(flagKey, context, defaultStage, default);
        }

        #endregion
    }
}
