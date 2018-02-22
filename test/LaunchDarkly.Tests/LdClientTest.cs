using System;
using LaunchDarkly.Client;
using Xunit;

namespace LaunchDarkly.Tests

{
    public class LdClientTest
    {
        [Fact]
        public void SecureModeHashTest()
        {
            Configuration config = Configuration.Default("secret");
            config.WithOffline(true);
            LdClient client = new LdClient(config);

            var user = User.WithKey("Message");
            Assert.Equal("aa747c502a898200f9e4fa21bac68136f886a0e27aec70ba06daf2e2a5cb5597", client.SecureModeHash(user));
            client.Dispose();
        }

        [Fact]
        public void DisposableIStoreEvents()
        {
            Configuration config = Configuration.Default("secret");
            config.WithOffline(true);

            MockDisposableStoreEvents storeEvents = new MockDisposableStoreEvents();

            using (LdClient client = new LdClient(config, storeEvents))
            {
            }

            Assert.True(storeEvents.Disposed);
        }

        private sealed class MockDisposableStoreEvents : IStoreEvents
        {
            public bool Disposed { get; private set; }

            void IDisposable.Dispose()
            {
                Disposed = true;
            }

            void IStoreEvents.Add(Event eventToLog)
            {
            }

            void IStoreEvents.Flush()
            {
            }
        }
    }
}