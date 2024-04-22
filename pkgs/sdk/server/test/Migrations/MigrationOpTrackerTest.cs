using System;
using System.Linq;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    public class MigrationOpTrackerTest : BaseTest
    {
        [Fact]
        public void ItCanMakeAMinimalEvent()
        {
            var tracker = BasicTracker();
            tracker.Op(MigrationOperation.Read);
            tracker.Invoked(MigrationOrigin.New);
            tracker.Invoked(MigrationOrigin.Old);
            var optEvent = tracker.CreateEvent();

            Assert.True(optEvent.HasValue);
            var migrationOpEvent = optEvent.Value;
            Assert.Equal(MigrationOperation.Read.ToDataModelString(), migrationOpEvent.Operation);
            Assert.Equal("my-flag", migrationOpEvent.FlagKey);
            Assert.Equal(1, migrationOpEvent.Variation);
            Assert.Equal(EvaluationReason.FallthroughReason, migrationOpEvent.Reason);
            Assert.Equal(MigrationStage.Live.ToDataModelString(), migrationOpEvent.Value.AsString);
            Assert.Equal(MigrationStage.Off.ToDataModelString(), migrationOpEvent.Default.AsString);
            Assert.Equal(Context.New("user-key"), migrationOpEvent.Context);

            Assert.True(migrationOpEvent.Invoked.Old);
            Assert.True(migrationOpEvent.Invoked.New);
            Assert.False(migrationOpEvent.Consistent.HasValue);
            Assert.False(migrationOpEvent.Error.HasValue);
            Assert.False(migrationOpEvent.Latency.HasValue);
            Assert.Empty(LogCapture.GetMessages());
        }

        [Fact]
        public void ItDoesNotCreateAnEventWhenTheFlagKeyIsEmpty()
        {
            var tracker = new MigrationOpTracker(
                MigrationStage.Live,
                MigrationStage.Off,
                "",
                new FeatureFlagBuilder("my-flag").Build(),
                Context.New("user-key"),
                1,
                TestLogger,
                new EvaluationDetail<string>("live", 1, EvaluationReason.FallthroughReason)
            );

            tracker.Op(MigrationOperation.Read);
            tracker.Invoked(MigrationOrigin.Old);
            var optEvent = tracker.CreateEvent();

            Assert.False(optEvent.HasValue);
        }

        [Theory]
        [InlineData(MigrationOperation.Read)]
        [InlineData(MigrationOperation.Write)]
        public void ItCorrectlyHandlesTheOperation(MigrationOperation operation)
        {
            var tracker = BasicTracker();
            tracker.Op(operation);
            tracker.Invoked(MigrationOrigin.New);
            tracker.Invoked(MigrationOrigin.Old);
            var optEvent = tracker.CreateEvent();

            Assert.True(optEvent.HasValue);
            Assert.Equal(operation.ToDataModelString(), optEvent.Value.Operation);
            Assert.Empty(LogCapture.GetMessages());
        }

        [Fact]
        public void ItMakesNoEventIfNoOpIsSet()
        {
            var tracker = BasicTracker();
            tracker.Invoked(MigrationOrigin.New);
            Assert.False(tracker.CreateEvent().HasValue);
            Assert.True(LogCapture.HasMessageWithText(LogLevel.Error, "The operation must be set, using \"op\"" +
                                                                      " before an event can be created."));
        }

        [Fact]
        public void ItMakesNoEventIfNoMethodWasInvoked()
        {
            var tracker = BasicTracker();
            tracker.Op(MigrationOperation.Read);
            Assert.False(tracker.CreateEvent().HasValue);
            Assert.True(LogCapture.HasMessageWithText(LogLevel.Error, "The migration invoked neither the \"old\" " +
                                                                      "or \"new\" implementation and an event cannot" +
                                                                      " be generated."));
        }

        [Fact]
        public void ItMakesNoEventIfTheContextIsNotValid()
        {
            var tracker = new MigrationOpTracker(
                MigrationStage.Live,
                MigrationStage.Off,
                "my-flag",
                new FeatureFlagBuilder("my-flag").Build(),
                Context.New(ContextKind.Of("multi"), "kind-kin"),
                1,
                TestLogger,
                new EvaluationDetail<string>("live", 1, EvaluationReason.FallthroughReason)
            );
            tracker.Op(MigrationOperation.Read);
            tracker.Invoked(MigrationOrigin.New);
            Assert.False(tracker.CreateEvent().HasValue);
            Assert.True(LogCapture.HasMessageWithText(LogLevel.Error, "The migration was not done against a" +
                                                                      " valid context and cannot generate an event."));
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public void ItHandlesAllValidCombinationsOfInvoked(bool oldInvoke, bool newInvoke)
        {
            var tracker = BasicTracker();
            tracker.Op(MigrationOperation.Read);
            if (oldInvoke)
            {
                tracker.Invoked(MigrationOrigin.Old);
            }

            if (newInvoke)
            {
                tracker.Invoked(MigrationOrigin.New);
            }

            var optEvent = tracker.CreateEvent();
            Assert.True(optEvent.HasValue);
            var migrationOpEvent = optEvent.Value;
            Assert.Equal(oldInvoke, migrationOpEvent.Invoked.Old);
            Assert.Equal(newInvoke, migrationOpEvent.Invoked.New);
            Assert.Empty(LogCapture.GetMessages());
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public void ItHandlesAllCombinationsOfErrors(bool oldError, bool newError)
        {
            var tracker = BasicTracker();
            tracker.Op(MigrationOperation.Read);
            tracker.Invoked(MigrationOrigin.New);
            tracker.Invoked(MigrationOrigin.Old);
            if (oldError)
            {
                tracker.Error(MigrationOrigin.Old);
            }

            if (newError)
            {
                tracker.Error(MigrationOrigin.New);
            }

            var optEvent = tracker.CreateEvent();
            Assert.True(optEvent.HasValue);
            var migrationOpEvent = optEvent.Value;
            if (oldError || newError)
            {
                Assert.True(migrationOpEvent.Error.HasValue);
                Assert.Equal(oldError, migrationOpEvent.Error?.Old);
                Assert.Equal(newError, migrationOpEvent.Error?.New);
            }
            else
            {
                Assert.False(migrationOpEvent.Error.HasValue);
            }
            Assert.Empty(LogCapture.GetMessages());
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public void ItHandlesAllCombinationsOfLatency(bool oldLatency, bool newLatency)
        {
            var tracker = BasicTracker();
            tracker.Op(MigrationOperation.Read);
            tracker.Invoked(MigrationOrigin.New);
            tracker.Invoked(MigrationOrigin.Old);
            if (oldLatency)
            {
                tracker.Latency(MigrationOrigin.Old, new TimeSpan(0,0,0,0,100));
            }

            if (newLatency)
            {
                tracker.Latency(MigrationOrigin.New, new TimeSpan(0,0,0,0,200));
            }

            var optEvent = tracker.CreateEvent();
            Assert.True(optEvent.HasValue);
            var migrationOpEvent = optEvent.Value;
            if (oldLatency || newLatency)
            {
                Assert.True(migrationOpEvent.Latency.HasValue);
                Assert.Equal(oldLatency ? new int?(100) : null, migrationOpEvent.Latency?.Old);
                Assert.Equal(newLatency ? new int?(200) : null, migrationOpEvent.Latency?.New);
            }
            else
            {
                Assert.False(migrationOpEvent.Latency.HasValue);
            }
            Assert.Empty(LogCapture.GetMessages());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItHandlesConsistencyChecksWhenSampled(bool checkResult)
        {
            var tracker = BasicTracker();
            tracker.Op(MigrationOperation.Read);
            tracker.Invoked(MigrationOrigin.New);
            tracker.Invoked(MigrationOrigin.Old);
            tracker.Consistency(() => checkResult);
            var optEvent = tracker.CreateEvent();
            Assert.True(optEvent.HasValue);
            var migrationOpEvent = optEvent.Value;
            Assert.Equal(checkResult, migrationOpEvent.Consistent?.IsConsistent);
            Assert.Equal(1, migrationOpEvent.Consistent?.SamplingRatio);
            Assert.Empty(LogCapture.GetMessages());
        }

        [Fact]
        public void ItDoesNotSampleConsistencyWhenTheCheckRatioIsZero()
        {
            var tracker = new MigrationOpTracker(
                MigrationStage.Live,
                MigrationStage.Off,
                "my-flag",
                new FeatureFlagBuilder("my-flag").Build(),
                Context.New("user-key"),
                0,
                TestLogger,
                new EvaluationDetail<string>("live", 1, EvaluationReason.FallthroughReason)
            );
            tracker.Op(MigrationOperation.Read);
            tracker.Invoked(MigrationOrigin.New);
            tracker.Invoked(MigrationOrigin.Old);
            var optEvent = tracker.CreateEvent();
            Assert.True(optEvent.HasValue);
            var migrationOpEvent = optEvent.Value;
            Assert.False(migrationOpEvent.Consistent.HasValue);
            Assert.Empty(LogCapture.GetMessages());
        }

        [Theory]
        [InlineData(MigrationOperation.Read, MigrationOrigin.Old)]
        [InlineData(MigrationOperation.Write, MigrationOrigin.Old)]
        [InlineData(MigrationOperation.Read, MigrationOrigin.New)]
        [InlineData(MigrationOperation.Write, MigrationOrigin.New)]
        public void ItReportsConsistencyErrorForComparisonWithoutBothMethods(MigrationOperation operation, MigrationOrigin origin)
        {
            var tracker = BasicTracker();
            tracker.Op(operation);
            tracker.Invoked(origin);
            tracker.Consistency(() => true);
            Assert.False(tracker.CreateEvent().HasValue);
            Assert.True(LogCapture.HasMessageWithRegex(LogLevel.Error, $".*{operation}.* Consistency check was done.*"));
        }

        [Theory]
        [InlineData(MigrationOperation.Read, MigrationOrigin.Old)]
        [InlineData(MigrationOperation.Write, MigrationOrigin.Old)]
        [InlineData(MigrationOperation.Read, MigrationOrigin.New)]
        [InlineData(MigrationOperation.Write, MigrationOrigin.New)]
        public void ItReportsAConsistencyErrorForErrorsWithoutInvoked(MigrationOperation operation, MigrationOrigin origin)
        {
            var tracker = BasicTracker();
            var oppositeOrigin = new[] {MigrationOrigin.New, MigrationOrigin.Old}.First(item => item != origin);
            tracker.Op(operation);
            tracker.Invoked(oppositeOrigin);
            tracker.Error(origin);
            Assert.False(tracker.CreateEvent().HasValue);
            Assert.True(LogCapture.HasMessageWithRegex(LogLevel.Error, $".*Error reported for {origin}.*was not invoked\\."));
        }

        [Theory]
        [InlineData(MigrationOperation.Read, MigrationOrigin.Old)]
        [InlineData(MigrationOperation.Write, MigrationOrigin.Old)]
        [InlineData(MigrationOperation.Read, MigrationOrigin.New)]
        [InlineData(MigrationOperation.Write, MigrationOrigin.New)]
        public void ItReportsAConsistencyErrorForLatencyWithoutInvoked(MigrationOperation operation, MigrationOrigin origin)
        {
            var tracker = BasicTracker();
            var oppositeOrigin = new[] {MigrationOrigin.New, MigrationOrigin.Old}.First(item => item != origin);
            tracker.Op(operation);
            tracker.Invoked(oppositeOrigin);
            tracker.Latency(origin, TimeSpan.Zero);
            Assert.False(tracker.CreateEvent().HasValue);
            Assert.True(LogCapture.HasMessageWithRegex(LogLevel.Error, $".*Latency was recorded for {origin}.*was not invoked\\."));
        }

        [Fact]
        public void ItHandlesExceptionsInConsistencyMethod()
        {
            var tracker = BasicTracker();
            tracker.Op(MigrationOperation.Read);
            tracker.Invoked(MigrationOrigin.New);
            tracker.Invoked(MigrationOrigin.Old);
            tracker.Consistency(() => throw new Exception("I AM A BAD COMPARISON METHOD"));
            var optEvent = tracker.CreateEvent();
            Assert.True(optEvent.HasValue);
            var migrationOpEvent = optEvent.Value;
            Assert.False(optEvent?.Consistent.HasValue);
            Assert.True(LogCapture.HasMessageWithRegex(LogLevel.Error, $"Exception executing migration" +
                                                                       $" comparison method:.*I AM A BAD COMPARISON METHOD.*"));
        }


        private MigrationOpTracker BasicTracker()
        {
            return new MigrationOpTracker(
                MigrationStage.Live,
                MigrationStage.Off,
                "my-flag",
                new FeatureFlagBuilder("my-flag").Build(),
                Context.New("user-key"),
                1,
                TestLogger,
                new EvaluationDetail<string>("live", 1, EvaluationReason.FallthroughReason)
            );
        }
    }
}
