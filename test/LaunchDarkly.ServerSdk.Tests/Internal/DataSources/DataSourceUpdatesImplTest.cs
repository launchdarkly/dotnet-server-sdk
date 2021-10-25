using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.TestHelpers;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using static LaunchDarkly.Sdk.Server.TestUtils;
using static LaunchDarkly.TestHelpers.Assertions;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class DataSourceUpdatesImplTest : BaseTest
    {
        private static readonly FeatureFlag flag1 = new FeatureFlagBuilder("flag1").Version(1).Build();
        private static readonly FeatureFlag flag1v2 = new FeatureFlagBuilder("flag1").Version(2).Build();
        private static readonly FeatureFlag flag2 = new FeatureFlagBuilder("flag2").Version(1).Build();
        private static readonly FeatureFlag flag2v2 = new FeatureFlagBuilder("flag2").Version(2).Build();
        private static readonly Segment segment1 = new SegmentBuilder("segment1").Version(1).Build();
        private static readonly Segment segment1v2 = new SegmentBuilder("segment1").Version(2).Build();
        private static readonly Segment segment2 = new SegmentBuilder("segment2").Version(1).Build();
        private static readonly Segment segment2v2 = new SegmentBuilder("segment2").Version(2).Build();

        private IDataStore store;
        private DataStoreUpdatesImpl dataStoreUpdates;
        private DataStoreStatusProviderImpl dataStoreStatusProvider;

        private DataSourceUpdatesImpl MakeInstance() =>
            new DataSourceUpdatesImpl(
                store,
                dataStoreStatusProvider,
                BasicTaskExecutor,
                TestLogger,
                null
                );

        public DataSourceUpdatesImplTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            store = new InMemoryDataStore();
            dataStoreUpdates = new DataStoreUpdatesImpl(BasicTaskExecutor, TestLogger);
            dataStoreStatusProvider = new DataStoreStatusProviderImpl(store, dataStoreUpdates);
        }

        [Fact]
        public void SendsEventsOnInitForNewlyAddedFlags()
        {
            var dataBuilder = new DataSetBuilder().Flags(flag1).Segments(segment1);

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            dataBuilder.Flags(flag2).Segments(segment2);
            updates.Init(dataBuilder.Build());

            ExpectFlagChangeEvents(eventSink, flag2.Key);
        }

        [Fact]
        public void SendsEventOnUpdateForNewlyAddedFlag()
        {
            var dataBuilder = new DataSetBuilder().Flags(flag1).Segments(segment1);

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            updates.Upsert(DataModel.Features, flag2.Key, DescriptorOf(flag2));

            ExpectFlagChangeEvents(eventSink, flag2.Key);
        }

        [Fact]
        public void SendsEventsOnInitForUpdatedFlag()
        {
            var dataBuilder = new DataSetBuilder().Flags(flag1, flag2).Segments(segment1, segment2);

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            dataBuilder.Flags(flag2v2) // modified flag
                .Segments(segment2v2); // modified segment, but it's irrelevant
            updates.Init(dataBuilder.Build());

            ExpectFlagChangeEvents(eventSink, flag2v2.Key);
        }

        [Fact]
        public void SendsEventOnUpdateForUpdatedFlag()
        {
            var dataBuilder = new DataSetBuilder().Flags(flag1, flag2).Segments(segment1, segment2);

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            updates.Upsert(DataModel.Features, flag2v2.Key, DescriptorOf(flag2v2));

            ExpectFlagChangeEvents(eventSink, flag2v2.Key);
        }

        [Fact]
        public void DoesNotSendEventOnUpdateIfItemWasNotReallyUpdated()
        {
            var dataBuilder = new DataSetBuilder().Flags(flag1, flag2);

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            updates.Upsert(DataModel.Features, flag2.Key, DescriptorOf(flag2));

            eventSink.ExpectNoValue();
        }

        [Fact]
        public void SendsEventsOnInitForDeletedFlags()
        {
            var dataBuilder = new DataSetBuilder().Flags(flag1, flag2).Segments(segment1);

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            dataBuilder.RemoveFlag(flag2.Key);
            updates.Init(dataBuilder.Build());

            ExpectFlagChangeEvents(eventSink, flag2.Key);
        }

        [Fact]
        public void SendsEventOnUpdateForDeletedFlag()
        {
            var dataBuilder = new DataSetBuilder().Flags(flag1, flag2).Segments(segment1);

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            updates.Upsert(DataModel.Features, flag2.Key, ItemDescriptor.Deleted(flag2v2.Version));

            ExpectFlagChangeEvents(eventSink, flag2.Key);
        }

        [Fact]
        public void SendsEventsOnInitForFlagsWhosePrerequisitesChanged()
        {
            var dataBuilder = new DataSetBuilder().Flags(
                new FeatureFlagBuilder("flag1").Version(1).Build(),
                new FeatureFlagBuilder("flag2").Version(1)
                    .Prerequisites(new Prerequisite("flag1", 0)).Build(),
                new FeatureFlagBuilder("flag3").Version(1).Build(),
                new FeatureFlagBuilder("flag4").Version(1)
                    .Prerequisites(new Prerequisite("flag1", 0)).Build(),
                new FeatureFlagBuilder("flag5").Version(1)
                    .Prerequisites(new Prerequisite("flag4", 0)).Build(),
                new FeatureFlagBuilder("flag6").Version(1).Build()
                );

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            dataBuilder.Flags(new FeatureFlagBuilder("flag1").Version(2).Build());
            updates.Init(dataBuilder.Build());

            ExpectFlagChangeEvents(eventSink, "flag1", "flag2", "flag4", "flag5");
        }

        [Fact]
        public void SendsEventsOnUpdateForFlagsWhosePrerequisitesChanged()
        {
            var dataBuilder = new DataSetBuilder().Flags(
                new FeatureFlagBuilder("flag1").Version(1).Build(),
                new FeatureFlagBuilder("flag2").Version(1)
                    .Prerequisites(new Prerequisite("flag1", 0)).Build(),
                new FeatureFlagBuilder("flag3").Version(1).Build(),
                new FeatureFlagBuilder("flag4").Version(1)
                    .Prerequisites(new Prerequisite("flag1", 0)).Build(),
                new FeatureFlagBuilder("flag5").Version(1)
                    .Prerequisites(new Prerequisite("flag4", 0)).Build(),
                new FeatureFlagBuilder("flag6").Version(1).Build()
                );

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            updates.Upsert(DataModel.Features, "flag1", DescriptorOf(
                new FeatureFlagBuilder("flag1").Version(2).Build()));

            ExpectFlagChangeEvents(eventSink, "flag1", "flag2", "flag4", "flag5");
        }

        [Fact]
        public void SendsEventsOnInitForFlagsWhoseSegmentsChanged()
        {
            var dataBuilder = new DataSetBuilder().Flags(
                new FeatureFlagBuilder("flag1").Version(1).Build(),
                new FeatureFlagBuilder("flag2").Version(1)
                    .Rules(
                        new RuleBuilder().Clauses(
                            new ClauseBuilder().Op("segmentMatch").Values(LdValue.Of(segment1.Key)).Build()
                        ).Build()
                    )
                    .Build(),
                new FeatureFlagBuilder("flag3").Version(1).Build(),
                new FeatureFlagBuilder("flag4").Version(1)
                    .Prerequisites(new Prerequisite("flag2", 0)).Build()
                )
                .Segments(segment1, segment2);

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            dataBuilder.Segments(segment1v2);
            updates.Init(dataBuilder.Build());

            ExpectFlagChangeEvents(eventSink, "flag2", "flag4");
        }

        [Fact]
        public void SendsEventsOnUpdateForFlagsWhoseSegmentsChanged()
        {
            var dataBuilder = new DataSetBuilder().Flags(
                new FeatureFlagBuilder("flag1").Version(1).Build(),
                new FeatureFlagBuilder("flag2").Version(1)
                    .Rules(
                        new RuleBuilder().Clauses(
                            new ClauseBuilder().Op("segmentMatch").Values(LdValue.Of(segment1.Key)).Build()
                        ).Build()
                    )
                    .Build(),
                new FeatureFlagBuilder("flag3").Version(1).Build(),
                new FeatureFlagBuilder("flag4").Version(1)
                    .Prerequisites(new Prerequisite("flag2", 0)).Build()
                )
                .Segments(segment1, segment2);

            var updates = MakeInstance();

            updates.Init(dataBuilder.Build());

            var eventSink = new EventSink<FlagChangeEvent>();
            updates.FlagChanged += eventSink.Add;

            updates.Upsert(DataModel.Segments, segment1.Key, DescriptorOf(segment1v2));

            ExpectFlagChangeEvents(eventSink, "flag2", "flag4");
        }

        [Fact]
        public void UpdateStatusBroadcastsNewStatus()
        {
            var updates = MakeInstance();
            var statuses = new EventSink<DataSourceStatus>();
            updates.StatusChanged += statuses.Add;

            var timeBeforeUpdate = DateTime.Now;
            var errorInfo = DataSourceStatus.ErrorInfo.FromHttpError(401);
            updates.UpdateStatus(DataSourceState.Off, errorInfo);

            var status = statuses.ExpectValue();
            Assert.Equal(DataSourceState.Off, status.State);
            Assert.InRange(status.StateSince, timeBeforeUpdate, timeBeforeUpdate.AddSeconds(1));
            Assert.Equal(errorInfo, status.LastError);
        }

        [Fact]
        public void UpdateStatusKeepsStateUnchangedIfStateWasInitializingAndNewStateIsInterrupted()
        {
            var updates = MakeInstance();

            Assert.Equal(DataSourceState.Initializing, updates.LastStatus.State);
            var originalTime = updates.LastStatus.StateSince;

            var statuses = new EventSink<DataSourceStatus>();
            updates.StatusChanged += statuses.Add;

            var errorInfo = DataSourceStatus.ErrorInfo.FromHttpError(401);
            updates.UpdateStatus(DataSourceState.Interrupted, errorInfo);

            var status = statuses.ExpectValue();
            Assert.Equal(DataSourceState.Initializing, status.State);
            Assert.Equal(originalTime, status.StateSince);
            Assert.Equal(errorInfo, status.LastError);
        }

        [Fact]
        public void UpdateStatusDoesNothingIfParametersHaveNoNewData()
        {
            var updates = MakeInstance();

            var statuses = new EventSink<DataSourceStatus>();
            updates.StatusChanged += statuses.Add;

            updates.UpdateStatus(DataSourceState.Initializing, null);

            statuses.ExpectNoValue();
        }

        [Fact]
        public void OutageTimeoutLogging()
        {
            var outageTimeout = TimeSpan.FromMilliseconds(100);

            var updates = new DataSourceUpdatesImpl(
                store,
                dataStoreStatusProvider,
                BasicTaskExecutor,
                TestLogger,
                outageTimeout
                );

            // simulate an outage
            updates.UpdateStatus(DataSourceState.Interrupted, DataSourceStatus.ErrorInfo.FromHttpError(500));

            // but recover from it immediately
            updates.UpdateStatus(DataSourceState.Valid, null);

            // wait until the timeout would have elapsed - no special message should be logged
            Thread.Sleep(outageTimeout.Add(TimeSpan.FromMilliseconds(50)));

            // simulate another outage
            updates.UpdateStatus(DataSourceState.Interrupted, DataSourceStatus.ErrorInfo.FromHttpError(501));
            updates.UpdateStatus(DataSourceState.Interrupted, DataSourceStatus.ErrorInfo.FromHttpError(502));
            updates.UpdateStatus(DataSourceState.Interrupted,
                DataSourceStatus.ErrorInfo.FromException(new IOException("x")));
            updates.UpdateStatus(DataSourceState.Interrupted, DataSourceStatus.ErrorInfo.FromHttpError(501));

            Thread.Sleep(outageTimeout);
            AssertEventually(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(50), () =>
                {
                    var messages = LogCapture.GetMessages().Where(m => m.Level == LogLevel.Error).ToList();
                    if (messages.Count == 1)
                    {
                        var m = messages[0];
                        if (m.LoggerName == ".DataSource" &&
                            m.Text.Contains("NETWORK_ERROR (1 time)") &&
                            m.Text.Contains("ERROR_RESPONSE(501) (2 times)") &&
                            m.Text.Contains("ERROR_RESPONSE(502) (1 time)"))
                        {
                            return true;
                        }
                    }
                    return false;
                }
                );
        }

        private void ExpectFlagChangeEvents(EventSink<FlagChangeEvent> eventSink, params string[] flagKeys)
        {
            var expectedChangedFlagKeys = new HashSet<string>(flagKeys);
            var actualChangedFlagKeys = new HashSet<string>();
            for (var i = 0; i < flagKeys.Length; i++)
            {
                if (eventSink.TryTakeValue(out var e))
                {
                    actualChangedFlagKeys.Add(e.Key);
                }
                else
                {
                    Assert.True(false, string.Format("expected flag change events: [{0}] but only got: [{1}]",
                        string.Join(", ", expectedChangedFlagKeys), string.Join(", ", actualChangedFlagKeys)));
                }
            }
            Assert.Equal(expectedChangedFlagKeys, actualChangedFlagKeys);
            eventSink.ExpectNoValue();
        }
    }
}
