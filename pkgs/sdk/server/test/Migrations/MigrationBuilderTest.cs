using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    public class MigrationBuilderTest
    {
        private readonly ILdClient _client = new LdClient(Configuration.Builder("").Offline(true).Build());
        [Fact]
        public void ItCanBuildAMinimumMigration()
        {
            var migration = new MigrationBuilder<string, string, string, string>(_client)
                .Read((payload) => MigrationMethod.Success("read old"),
                    (payload) => MigrationMethod.Success("read new"))
                .Write((payload) => MigrationMethod.Success("write old"),
                    (payload) => MigrationMethod.Success("write new"))
                .Build();
            Assert.NotNull(migration);
        }

        [Fact]
        public void ItCanBuildAMigrationWithACheckFunction()
        {
            var migration = new MigrationBuilder<string, string, string, string>(_client)
                .Read((payload) => MigrationMethod.Success("read old"),
                    (payload) => MigrationMethod.Success("read new"),
                    (a, b) => a.Equals(b))
                .Write((payload) => MigrationMethod.Success("write old"),
                    (payload) => MigrationMethod.Success("write new"))
                .Build();
            Assert.NotNull(migration);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItCanConstructAMigrationWithLatencyTrackingOnAndOff(bool enabled)
        {
            var migration = new MigrationBuilder<string, string, string, string>(_client)
                .TrackLatency(enabled)
                .Read((payload) => MigrationMethod.Success("read old"),
                    (payload) => MigrationMethod.Success("read new"))
                .Write((payload) => MigrationMethod.Success("write old"),
                    (payload) => MigrationMethod.Success("write new"))
                .Build();
            Assert.NotNull(migration);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItCanConstructAMigrationWitErrorTrackingOnAndOff(bool enabled)
        {
            var migration = new MigrationBuilder<string, string, string, string>(_client)
                .TrackErrors(enabled)
                .Read((payload) => MigrationMethod.Success("read old"),
                    (payload) => MigrationMethod.Success("read new"))
                .Write((payload) => MigrationMethod.Success("write old"),
                    (payload) => MigrationMethod.Success("write new"))
                .Build();
            Assert.NotNull(migration);
        }

        public static IEnumerable<object[]> GetExecution()
        {
            yield return new object[] {MigrationExecution.Parallel()};
            yield return new object[] {MigrationExecution.Serial(MigrationSerialOrder.Fixed)};
            yield return new object[] {MigrationExecution.Serial(MigrationSerialOrder.Random)};
        }

        [Theory]
        [MemberData(nameof(GetExecution))]
        public void ItCanConstructAMigrationWithDifferentExecutionMethods(MigrationExecution execution)
        {
            var migration = new MigrationBuilder<string, string, string, string>(_client)
                .ReadExecution(execution)
                .Read((payload) => MigrationMethod.Success("read old"),
                    (payload) => MigrationMethod.Success("read new"))
                .Write((payload) => MigrationMethod.Success("write old"),
                    (payload) => MigrationMethod.Success("write new"))
                .Build();
            Assert.NotNull(migration);
        }

        [Fact]
        public void ItDoesNotBuildIfNoReadMethodsAreSet()
        {
            var migration = new MigrationBuilder<string, string, string, string>(_client)
                .Write((payload) => new MigrationMethod.Result<string>(),
                    (payload) => new MigrationMethod.Result<string>())
                .Build();
            Assert.Null(migration);
        }

        [Fact]
        public void ItDoesNotBuildIfNoWriteMethodsAreSet()
        {
            var migration = new MigrationBuilder<string, string, string, string>(_client)
                .Read((payload) => new MigrationMethod.Result<string>(),
                    (payload) => new MigrationMethod.Result<string>())
                .Build();
            Assert.Null(migration);
        }
    }
}
