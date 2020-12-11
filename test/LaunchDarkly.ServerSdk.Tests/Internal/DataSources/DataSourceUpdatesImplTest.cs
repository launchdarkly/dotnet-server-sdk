using System;
using System.IO;
using System.Threading;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class DataSourceUpdatesImplTest : BaseTest
    {
        private DataSourceUpdatesImpl MakeInstance(IDataStore store) =>
            new DataSourceUpdatesImpl(store, new TaskExecutor(testLogger),
                testLogger, null);

        public DataSourceUpdatesImplTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void UpdateStatusBroadcastsNewStatus()
        {
            var updates = MakeInstance(new InMemoryDataStore());
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
            var updates = MakeInstance(new InMemoryDataStore());

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
            var updates = MakeInstance(new InMemoryDataStore());

            var statuses = new EventSink<DataSourceStatus>();
            updates.StatusChanged += statuses.Add;

            updates.UpdateStatus(DataSourceState.Initializing, null);

            statuses.ExpectNoValue();
        }

        [Fact]
        public void OutageTimeoutLogging()
        {
            var logCapture = Logs.Capture();
            var outageTimeout = TimeSpan.FromMilliseconds(100);

            var logger = logCapture.Logger("logname");
            var updates = new DataSourceUpdatesImpl(new InMemoryDataStore(),
                new TaskExecutor(logger), logger, outageTimeout);

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

            Thread.Sleep(outageTimeout.Add(TimeSpan.FromMilliseconds(100)));
            var messages = logCapture.GetMessages();
            Assert.Collection(messages,
                m =>
                {
                    Assert.Equal(LogLevel.Error, m.Level);
                    Assert.Equal("logname.DataSource", m.LoggerName);
                    Assert.Contains("NETWORK_ERROR (1 time)", m.Text);
                    Assert.Contains("ERROR_RESPONSE(501) (2 times)", m.Text);
                    Assert.Contains("ERROR_RESPONSE(502) (1 time)", m.Text);
                });
            Assert.NotEmpty(messages);
        }
    }
}
