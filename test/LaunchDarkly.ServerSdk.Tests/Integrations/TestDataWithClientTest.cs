using LaunchDarkly.Sdk.Server.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class TestDataWithClientTest : BaseTest
    {
        private readonly TestData _td = TestData.DataSource();
        private readonly Configuration _config;
        private readonly User _user = User.WithKey("userkey");

        public TestDataWithClientTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            _config = BasicConfig()
                .DataSource(_td)
                .Events(Components.NoEvents)
                .Build();
        }

        [Fact]
        public void InitializesWithEmptyData()
        {
            using (var client = new LdClient(_config))
            {
                Assert.True(client.Initialized);
            }
        }

        [Fact]
        public void InitializesWithFlag()
        {
            _td.Update(_td.Flag("flag").On(true));

            using (var client = new LdClient(_config))
            {
                Assert.True(client.BoolVariation("flag", _user, false));
            }
        }

        [Fact]
        public void UpdatesFlag()
        {
            using (var client = new LdClient(_config))
            {
                Assert.False(client.BoolVariation("flag", _user, false));

                _td.Update(_td.Flag("flag").On(true));

                Assert.True(client.BoolVariation("flag", _user, false));
            }
        }

        [Fact]
        public void UsesTargets()
        {
            _td.Update(_td.Flag("flag").FallthroughVariation(false).VariationForUser("user1", true));

            using (var client = new LdClient(_config))
            {
                Assert.True(client.BoolVariation("flag", User.WithKey("user1"), false));
                Assert.False(client.BoolVariation("flag", User.WithKey("user2"), false));
            }
        }

        [Fact]
        public void UsesRules()
        {
            _td.Update(_td.Flag("flag").FallthroughVariation(false)
                .IfMatch(UserAttribute.Name, LdValue.Of("Lucy")).ThenReturn(true)
                .IfMatch(UserAttribute.Name, LdValue.Of("Mina")).ThenReturn(true));

            using (var client = new LdClient(_config))
            {
                Assert.True(client.BoolVariation("flag", User.Builder("user1").Name("Lucy").Build(), false));
                Assert.True(client.BoolVariation("flag", User.Builder("user2").Name("Mina").Build(), false));
                Assert.False(client.BoolVariation("flag", User.Builder("user3").Name("Quincy").Build(), false));
            }
        }

        [Fact]
        public void NonBooleanFlags()
        {
            _td.Update(_td.Flag("flag").Variations(LdValue.Of("red"), LdValue.Of("green"), LdValue.Of("blue"))
                .OffVariation(0).FallthroughVariation(2)
                .VariationForUser("user1", 1)
                .IfMatch(UserAttribute.Name, LdValue.Of("Mina")).ThenReturn(1));

            using (var client = new LdClient(_config))
            {
                Assert.Equal("green", client.StringVariation("flag", User.Builder("user1").Name("Lucy").Build(), ""));
                Assert.Equal("green", client.StringVariation("flag", User.Builder("user2").Name("Mina").Build(), ""));
                Assert.Equal("blue", client.StringVariation("flag", User.Builder("user3").Name("Quincy").Build(), ""));

                _td.Update(_td.Flag("flag").On(false));

                Assert.Equal("red", client.StringVariation("flag", User.Builder("user1").Name("Lucy").Build(), ""));
            }
        }

        [Fact]
        public void CanUpdateStatus()
        {
            using (var client = new LdClient(_config))
            {
                Assert.Equal(DataSourceState.Valid, client.DataSourceStatusProvider.Status.State);

                var ei = DataSourceStatus.ErrorInfo.FromHttpError(500);
                _td.UpdateStatus(DataSourceState.Interrupted, ei);

                Assert.Equal(DataSourceState.Interrupted, client.DataSourceStatusProvider.Status.State);
                Assert.Equal(ei, client.DataSourceStatusProvider.Status.LastError);
            }
        }

        [Fact]
        public void DataSourcePropagatesToMultipleClients()
        {
            _td.Update(_td.Flag("flag").On(true));

            using (var client1 = new LdClient(_config))
            {
                using (var client2 = new LdClient(_config))
                {
                    Assert.True(client1.BoolVariation("flag", _user, false));
                    Assert.True(client2.BoolVariation("flag", _user, false));

                    _td.Update(_td.Flag("flag").On(false));

                    Assert.False(client1.BoolVariation("flag", _user, false));
                    Assert.False(client2.BoolVariation("flag", _user, false));
                }
            }
        }
    }
}
