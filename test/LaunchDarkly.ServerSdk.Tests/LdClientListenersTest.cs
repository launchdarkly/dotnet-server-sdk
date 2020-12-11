using System;
using System.Collections.Concurrent;
using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientListenersTest : BaseTest
    {
        public LdClientListenersTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void DataSourceStatusProviderReturnsLatestStatus()
        {
            var dataSourceFactory = new CapturingDataSourceFactory();
            var config = Configuration.Builder("")
                .DataSource(dataSourceFactory)
                .Events(Components.NoEvents)
                .Logging(Components.Logging(testLogging))
                .Build();
            var timeBeforeStarting = DateTime.Now;

            using (var client = new LdClient(config))
            {
                var initialStatus = client.DataSourceStatusProvider.Status;
                Assert.Equal(DataSourceState.Initializing, initialStatus.State);
                Assert.InRange(initialStatus.StateSince, timeBeforeStarting, timeBeforeStarting.AddSeconds(1));
                Assert.Null(initialStatus.LastError);

                var errorInfo = DataSourceStatus.ErrorInfo.FromHttpError(401);
                dataSourceFactory.DataSourceUpdates.UpdateStatus(DataSourceState.Off, errorInfo);

                var newStatus = client.DataSourceStatusProvider.Status;
                Assert.Equal(DataSourceState.Off, newStatus.State);
                Assert.InRange(newStatus.StateSince, errorInfo.Time, errorInfo.Time.AddSeconds(1));
                Assert.Equal(errorInfo, newStatus.LastError);
            }
        }

        [Fact]
        public void DataSourceStatusProviderSendsStatusUpdates()
        {
            var dataSourceFactory = new CapturingDataSourceFactory();
            var config = Configuration.Builder("")
                .DataSource(dataSourceFactory)
                .Logging(Components.Logging(testLogging))
                .Events(Components.NoEvents).Build();
            
            using (var client = new LdClient(config))
            {
                var statuses = new EventSink<DataSourceStatus>();
                client.DataSourceStatusProvider.StatusChanged += statuses.Add;

                var errorInfo = DataSourceStatus.ErrorInfo.FromHttpError(401);
                dataSourceFactory.DataSourceUpdates.UpdateStatus(DataSourceState.Off, errorInfo);

                var newStatus = statuses.ExpectValue();
                Assert.Equal(DataSourceState.Off, newStatus.State);
                Assert.InRange(newStatus.StateSince, errorInfo.Time, errorInfo.Time.AddSeconds(1));
                Assert.Equal(errorInfo, newStatus.LastError);
            }
        }
    }
}
