using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using LaunchDarkly.Sdk.Server.Subsystems;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    [SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
    public class MigrationTest
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public MigrationTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public readonly struct Expectations
        {
            public bool ReadOld { get; }
            public bool ReadNew { get; }
            public bool WriteOld { get; }
            public bool WriteNew { get; }

            public Expectations(bool readOld, bool readNew, bool writeOld, bool writeNew)
            {
                ReadOld = readOld;
                ReadNew = readNew;
                WriteOld = writeOld;
                WriteNew = writeNew;
            }

            public override string ToString()
            {
                return $"ReadOld({ReadOld}), ReadNew({ReadNew}), WriteOld({WriteOld}), WriteNew({WriteNew})";
            }
        }

        private class ExecutionOrders : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                // Mode, Stage, Expectations(read old, read new, write old, write new).
                yield return new object[]
                {
                    MigrationExecution.Parallel(), MigrationStage.Off,
                    new Expectations(true, false, true, false)
                };
                yield return new object[]
                {
                    MigrationExecution.Parallel(), MigrationStage.DualWrite,
                    new Expectations(true, false, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Parallel(), MigrationStage.Shadow,
                    new Expectations(true, true, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Parallel(), MigrationStage.Live,
                    new Expectations(true, true, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Parallel(), MigrationStage.RampDown,
                    new Expectations(false, true, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Parallel(), MigrationStage.Complete,
                    new Expectations(false, true, false, true)
                };

                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Fixed), MigrationStage.Off,
                    new Expectations(true, false, true, false)
                };
                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Fixed), MigrationStage.DualWrite,
                    new Expectations(true, false, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Fixed), MigrationStage.Shadow,
                    new Expectations(true, true, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Fixed), MigrationStage.Live,
                    new Expectations(true, true, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Fixed), MigrationStage.RampDown,
                    new Expectations(false, true, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Fixed), MigrationStage.Complete,
                    new Expectations(false, true, false, true)
                };

                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Random), MigrationStage.Off,
                    new Expectations(true, false, true, false)
                };
                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Random), MigrationStage.DualWrite,
                    new Expectations(true, false, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Random), MigrationStage.Shadow,
                    new Expectations(true, true, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Random), MigrationStage.Live,
                    new Expectations(true, true, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Random), MigrationStage.RampDown,
                    new Expectations(false, true, true, true)
                };
                yield return new object[]
                {
                    MigrationExecution.Serial(MigrationSerialOrder.Random), MigrationStage.Complete,
                    new Expectations(false, true, false, true)
                };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private class ExecutionOrdersWithExceptions: IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                foreach (var order in new ExecutionOrders())
                {
                    var updated = new object[order.Length + 1];
                    Array.Copy(order, updated, order.Length);
                    updated[updated.Length - 1] = false;
                    yield return updated;
                }

                foreach (var order in new ExecutionOrders())
                {
                    var updated = new object[order.Length + 1];
                    Array.Copy(order, updated, order.Length);
                    updated[updated.Length - 1] = true;
                    yield return updated;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private void AssertReads(BasicMigrationExecutor executor, Expectations expected)
        {
            Assert.True(expected.ReadOld == executor.ReadOldCalled, $"Expected read old to be {expected.ReadOld}");
            Assert.True(expected.ReadNew == executor.ReadNewCalled, $"Expected read new to be {expected.ReadNew}");
            // For a read there should be no writes.
            Assert.False(executor.WriteOldCalled, $"Expected no write to old");
            Assert.False(executor.WriteNewCalled, $"Expected no write to new");
        }

        private static void AssertWrites(BasicMigrationExecutor executor, Expectations expected)
        {
            Assert.True(expected.WriteOld == executor.WriteOldCalled, $"Expected write old to be {expected.WriteOld}");
            Assert.True(expected.WriteNew == executor.WriteNewCalled, $"Expected write new to be {expected.WriteNew}");
            // For a write there should be no reads.
            Assert.False(executor.ReadOldCalled, $"Expected no read to old");
            Assert.False(executor.ReadNewCalled, $"Expected no read to new");
        }

        private static void AssertReadOrigin(MigrationResult<string> res, MigrationStage stage)
        {
            switch (stage)
            {
                case MigrationStage.Off:
                case MigrationStage.DualWrite:
                case MigrationStage.Shadow:
                    Assert.True(res.Origin == MigrationOrigin.Old, "Expected it read from old, but it did not.");
                    break;
                case MigrationStage.Live:
                case MigrationStage.RampDown:
                case MigrationStage.Complete:
                    Assert.True(res.Origin == MigrationOrigin.New, "Expected it read from new, but it did not.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        private static MigrationOrigin AuthoritativeOrigin(MigrationStage stage)
        {
            switch (stage)
            {
                case MigrationStage.Off:
                case MigrationStage.DualWrite:
                case MigrationStage.Shadow:
                    return MigrationOrigin.Old;
                case MigrationStage.Live:
                case MigrationStage.RampDown:
                case MigrationStage.Complete:
                default:
                    return MigrationOrigin.New;
            }
        }

        private static void AssertWriteOrigin(MigrationWriteResult<string> res, MigrationStage stage)
        {
            switch (stage)
            {
                case MigrationStage.Off:
                    Assert.True(res.Authoritative.Origin == MigrationOrigin.Old,
                        "Expected authoritative origin to be old");
                    break;
                case MigrationStage.DualWrite: // Dual write and shadow do the same thing.
                case MigrationStage.Shadow:
                    Assert.True(res.Authoritative.Origin == MigrationOrigin.Old,
                        "Expected authoritative origin to be old");
                    Assert.True(res.NonAuthoritative?.Origin == MigrationOrigin.New,
                        "Expected non-authoritative origin to be new");
                    break;
                case MigrationStage.Live: // Live and ramp down do the same thing.
                case MigrationStage.RampDown:
                    Assert.True(res.Authoritative.Origin == MigrationOrigin.New,
                        "Expected authoritative origin to be new");
                    Assert.True(res.NonAuthoritative?.Origin == MigrationOrigin.Old,
                        "Expected non-authoritative origin to be old");
                    break;
                case MigrationStage.Complete:
                    Assert.True(res.Authoritative.Origin == MigrationOrigin.New,
                        "Expected authoritative origin to be new");
                    break;
            }
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItReadsFromTheCorrectOrigins(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            var executor = new BasicMigrationExecutor(execution);
            executor.SetStage(stage);
            var res = executor.Migration.Read(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));
            AssertReads(executor, expected);
            AssertReadOrigin(res, stage);
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItDoesNotReportErrorsForReadsWhenThereAreNone(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            var executor = new BasicMigrationExecutor(execution);
            executor.SetStage(stage);
            var res = executor.Migration.Read(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            Assert.False(mopEvent?.Error.HasValue);
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItDoesNotReportErrorsForWritesWhenThereAreNone(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            var executor = new BasicMigrationExecutor(execution);
            executor.SetStage(stage);
            var res = executor.Migration.Write(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            Assert.False(mopEvent?.Error.HasValue);
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItWritesToTheCorrectOrigins(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            var executor = new BasicMigrationExecutor(execution);
            _testOutputHelper.WriteLine("STAGE " + stage);
            executor.SetStage(stage);
            var res = executor.Migration.Write(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));
            AssertWrites(executor, expected);
            AssertWriteOrigin(res, stage);
        }

        [Theory]
        [ClassData(typeof(ExecutionOrdersWithExceptions))]
        public void ItReportsReadErrorsCorrectlyForOldReads(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected, bool useException)
        {
            var executor = new BasicMigrationExecutor(execution)
            {
                FailOldRead = !useException,
                ExceptionOldRead = useException
            };
            executor.SetStage(stage);
            var res = executor.Migration.Read(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));
            if (AuthoritativeOrigin(stage) == MigrationOrigin.Old)
            {
                Assert.False(res.IsSuccessful);
            }
            else
            {
                //Reads from new should still be fine.
                Assert.True(res.IsSuccessful);
            }

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            // If the old method was executed, then there should be an error reported in the event.
            if (expected.ReadOld)
            {
                Assert.True(mopEvent?.Error?.Old);
                Assert.False(mopEvent?.Error?.New);
            }
            else
            {
                Assert.False(mopEvent?.Error.HasValue);
            }
        }

        [Theory]
        [ClassData(typeof(ExecutionOrdersWithExceptions))]
        public void ItReportsReadErrorsCorrectlyForNewReads(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected, bool withException)
        {
            var executor = new BasicMigrationExecutor(execution)
            {
                FailNewRead = !withException,
                ExceptionNewRead = withException
            };
            executor.SetStage(stage);
            var res = executor.Migration.Read(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));
            if (AuthoritativeOrigin(stage) == MigrationOrigin.New)
            {
                Assert.False(res.IsSuccessful);
            }
            else
            {
                //Reads from old should still be fine.
                Assert.True(res.IsSuccessful);
            }

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            // If the new method was executed, then there should be an error reported in the event.
            if (expected.ReadNew)
            {
                Assert.False(mopEvent?.Error?.Old);
                Assert.True(mopEvent?.Error?.New);
            }
            else
            {
                Assert.False(mopEvent?.Error.HasValue);
            }
        }

        [Theory]
        [ClassData(typeof(ExecutionOrdersWithExceptions))]
        public void ItReportsReadErrorsCorrectlyForBothReads(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected, bool withException)
        {
            var executor = new BasicMigrationExecutor(execution)
            {
                FailNewRead = !withException,
                FailOldRead = !withException,
                ExceptionNewRead = withException,
                ExceptionOldRead = withException
            };
            executor.SetStage(stage);
            var res = executor.Migration.Read(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));
            Assert.False(res.IsSuccessful);
            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);
            // Any executed method should have an error.
            if (expected.ReadOld)
            {
                Assert.True(mopEvent?.Error?.Old);
            }
            else
            {
                Assert.False(mopEvent?.Error?.Old);
            }

            if (expected.ReadNew)
            {
                Assert.True(mopEvent?.Error?.New);
            }
            else
            {
                Assert.False(mopEvent?.Error?.New);
            }
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItAddsTheCorrectInvokedMeasurementsForReads(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            var executor = new BasicMigrationExecutor(execution);
            executor.SetStage(stage);
            executor.Migration.Read(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            if (expected.ReadOld)
            {
                Assert.True(mopEvent?.Invoked.Old);
            }
            else
            {
                Assert.False(mopEvent?.Invoked.Old);
            }

            if (expected.ReadNew)
            {
                Assert.True(mopEvent?.Invoked.New);
            }
            else
            {
                Assert.False(mopEvent?.Invoked.New);
            }
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItReportsLatencyForReads(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            var executor = new BasicMigrationExecutor(execution);
            executor.SetStage(stage);
            executor.Migration.Read(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            if (expected.ReadOld)
            {
                Assert.True(mopEvent?.Latency?.Old.HasValue);
            }
            else
            {
                Assert.False(mopEvent?.Latency?.Old.HasValue);
            }

            if (expected.ReadNew)
            {
                Assert.True(mopEvent?.Latency?.New.HasValue);
            }
            else
            {
                Assert.False(mopEvent?.Latency?.New.HasValue);
            }
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItReportsLatencyForWrites(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            var executor = new BasicMigrationExecutor(execution);
            executor.SetStage(stage);
            executor.Migration.Write(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            if (expected.WriteOld)
            {
                Assert.True(mopEvent?.Latency?.Old.HasValue);
            }
            else
            {
                Assert.False(mopEvent?.Latency?.Old.HasValue);
            }

            if (expected.WriteNew)
            {
                Assert.True(mopEvent?.Latency?.New.HasValue);
            }
            else
            {
                Assert.False(mopEvent?.Latency?.New.HasValue);
            }
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItForwardsThePayloadToReads(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            var executor = new BasicMigrationExecutor(execution);
            executor.SetStage(stage);
            executor.Migration.Read(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage), "the-payload");
            if (expected.ReadOld)
            {
                Assert.Equal("the-payload", executor.PayloadReadOld);
            }

            if (expected.ReadNew)
            {
                Assert.Equal("the-payload", executor.PayloadReadNew);
            }
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItForwardsThePayloadToWrites(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            var executor = new BasicMigrationExecutor(execution);
            executor.SetStage(stage);
            executor.Migration.Write(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage), "the-payload");
            if (expected.WriteOld)
            {
                Assert.Equal("the-payload", executor.PayloadWriteOld);
            }

            if (expected.WriteNew)
            {
                Assert.Equal("the-payload", executor.PayloadWriteNew);
            }
        }

        [Theory]
        [InlineData(MigrationStage.DualWrite, false)]
        [InlineData(MigrationStage.Shadow, false)]
        [InlineData(MigrationStage.DualWrite, true)]
        [InlineData(MigrationStage.Shadow, true)]
        public void ItStopsWritingWhenItEncountersAnErrorInOldWrite(MigrationStage stage, bool useException)
        {
            // Execution order does not matter for writes.
            var executor = new BasicMigrationExecutor(MigrationExecution.Parallel());
            executor.SetStage(stage);
            executor.FailOldWrite = !useException;
            executor.ExceptionOldWrite = useException;
            var writeResult = executor.Migration.Write(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));


            Assert.False(writeResult.Authoritative.IsSuccessful);
            Assert.False(writeResult.NonAuthoritative.HasValue);

            // The old write should fail, so the new write will not be done.
            Assert.True(executor.WriteOldCalled);
            Assert.False(executor.WriteNewCalled);

            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            Assert.False(mopEvent?.Invoked.New);
            Assert.True(mopEvent?.Invoked.Old);

            Assert.True(mopEvent?.Error.HasValue);
            Assert.False(mopEvent?.Error?.New);
            Assert.True(mopEvent?.Error?.Old);
        }

        [Theory]
        [InlineData(MigrationStage.Live, false)]
        [InlineData(MigrationStage.RampDown, false)]
        [InlineData(MigrationStage.Live, true)]
        [InlineData(MigrationStage.RampDown, true)]
        public void ItStopsWritingWhenItEncountersAnErrorInNewWrite(MigrationStage stage, bool useException)
        {
            // Execution order does not matter for writes.
            var executor = new BasicMigrationExecutor(MigrationExecution.Parallel());
            executor.SetStage(stage);
            executor.FailNewWrite = !useException;
            executor.ExceptionNewWrite = useException;
            var writeResult = executor.Migration.Write(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));


            Assert.False(writeResult.Authoritative.IsSuccessful);
            Assert.False(writeResult.NonAuthoritative.HasValue);

            // The old write should fail, so the new write will not be done.
            Assert.False(executor.WriteOldCalled);
            Assert.True(executor.WriteNewCalled);

            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            Assert.True(mopEvent?.Invoked.New);
            Assert.False(mopEvent?.Invoked.Old);

            Assert.True(mopEvent?.Error.HasValue);
            Assert.True(mopEvent?.Error?.New);
            Assert.False(mopEvent?.Error?.Old);
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItDoesNotReportLatencyForReadsIfLatencyTrackingDisabled(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            // Do not track latency.
            var executor = new BasicMigrationExecutor(execution, false);
            executor.SetStage(stage);
            executor.Migration.Read(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            Assert.False(mopEvent?.Latency.HasValue);
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItDoesNotReportLatencyForWritesIfLatencyTrackingDisabled(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            // Do not track latency.
            var executor = new BasicMigrationExecutor(execution, false);
            executor.SetStage(stage);
            executor.Migration.Write(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);
            Assert.False(mopEvent?.Latency.HasValue);
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItDoesNotReportErrorsForReadsIfErrorTrackingDisabled(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            // Do not track Errors.
            var executor = new BasicMigrationExecutor(execution, false);
            executor.SetStage(stage);
            executor.Migration.Read(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);

            Assert.False(mopEvent?.Error.HasValue);
        }

        [Theory]
        [ClassData(typeof(ExecutionOrders))]
        public void ItDoesNotReportErrorsForWritesIfErrorTrackingDisabled(
            MigrationExecution execution,
            MigrationStage stage, Expectations expected)
        {
            // Do not track Errors.
            var executor = new BasicMigrationExecutor(execution, false);
            executor.SetStage(stage);
            executor.Migration.Write(executor.FlagKey, Context.New("user-key"),
                BasicMigrationExecutor.GetDefaultStage(stage));

            // After feature event.
            var mopEvent = executor.EventSink.Events[1] as EventProcessorTypes.MigrationOpEvent?;
            Assert.True(mopEvent.HasValue);
            Assert.False(mopEvent?.Error.HasValue);
        }
    }
}
