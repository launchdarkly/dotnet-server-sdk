using System;
using System.Linq;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Subsystems;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Migrations
{
    public class BasicMigrationExecutor : BaseTest
    {
        public TestData TestData { get; } = TestData.DataSource();
        public string FlagKey => "test-flag";

        public MockEventProcessor EventSink { get; } = new MockEventProcessor();
        public readonly ILdClient Client;

        public IMigration<string, string, string, string> Migration;

        public bool ReadOldCalled { get; set; }
        public bool WriteOldCalled { get; set; }
        public bool ReadNewCalled { get; set; }
        public bool WriteNewCalled { get; set; }

        public bool FailOldWrite { get; set; }

        public bool FailNewWrite { get; set; }

        public bool FailOldRead { get; set; }

        public bool FailNewRead { get; set; }

        public bool ExceptionOldWrite { get; set; }
        public bool ExceptionNewWrite { get; set; }
        public bool ExceptionOldRead { get; set; }
        public bool ExceptionNewRead { get; set; }

        public string PayloadReadOld { get; set; }
        public string PayloadReadNew { get; set; }

        public string PayloadWriteOld { get; set; }
        public string PayloadWriteNew { get; set; }

        public BasicMigrationExecutor(MigrationExecution execution, bool trackLatency = true,
            bool trackErrors = true)
        {
            Client = new LdClient(BasicConfig()
                .Events(EventSink.AsSingletonFactory<IEventProcessor>())
                .DataSource(TestData)
                .Build());
            var builder =
                new MigrationBuilder<string, string, string, string>(Client).ReadExecution(execution)
                    .TrackLatency(trackLatency)
                    .TrackErrors(trackErrors)
                    .Read((payload) =>
                    {
                        ReadOldCalled = true;
                        PayloadReadOld = payload;
                        if (ExceptionOldRead)
                        {
                            throw new Exception("DISAPPOINTMENT");
                        }
                        return FailOldRead ? MigrationMethod.Failure<string>() : MigrationMethod.Success("Old");
                    }, (payload) =>
                    {
                        ReadNewCalled = true;
                        PayloadReadNew = payload;
                        if (ExceptionNewRead)
                        {
                            throw new Exception("DISAPPOINTMENT");
                        }
                        return FailNewRead ? MigrationMethod.Failure<string>() : MigrationMethod.Success("New");
                    })
                    .Write((payload) =>
                    {
                        WriteOldCalled = true;
                        PayloadWriteOld = payload;
                        if (ExceptionOldWrite)
                        {
                            throw new Exception("DISAPPOINTMENT");
                        }
                        return FailOldWrite ? MigrationMethod.Failure<string>() : MigrationMethod.Success("Old");
                    }, (payload) =>
                    {
                        WriteNewCalled = true;
                        PayloadWriteNew = payload;
                        if (ExceptionNewWrite)
                        {
                            throw new Exception("DISAPPOINTMENT");
                        }
                        return FailNewWrite ? MigrationMethod.Failure<string>() : MigrationMethod.Success("New");
                    });
            var built = builder.Build();
            Assert.NotNull(built);
            Migration = built;
        }

        /// <summary>
        /// Set the stage for the flag under test.
        /// </summary>
        /// <param name="stage">the stage the flag should evaluate to</param>
        public void SetStage(MigrationStage stage)
        {
            TestData.Update(TestData.Flag(FlagKey)
                .ValueForAll(LdValue.Of(stage.ToDataModelString())));
        }

        /// <summary>
        /// Get a stage that is not the expected stage.
        /// </summary>
        /// <param name="expectedStage">the stage to get a default for</param>
        /// <returns>a stage that is not the passed stage</returns>
        public static MigrationStage GetDefaultStage(MigrationStage expectedStage)
        {
            return new[]
            {
                MigrationStage.Off, MigrationStage.DualWrite, MigrationStage.Shadow,
                MigrationStage.RampDown, MigrationStage.Complete
            }.First(inStage => inStage != expectedStage);
        }
    }
}
