using Moq;
using LaunchDarkly.Client;
using System.Net.Http;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class RecordEventsTest
    {
//        [Fact]
        public void CanRaiseACustomEvent()
        {
            //TODO: this doesn't actually mock the client.
            var mockConfig = new Mock<Configuration>();
            mockConfig.Setup(x => x.BaseUri).Returns(Configuration.DefaultUri);
            mockConfig.Setup(x => x.SdkKey).Returns("fakeSdkKey");
            var mockHttpClient = new Mock<HttpClient>();
            mockConfig.Setup(x => x.HttpClient()).Returns(mockHttpClient.Object);

            var eventStore = new Mock<IStoreEvents>();
            var client = new LdClient(mockConfig.Object, eventStore.Object);
            var user = User.WithKey("user@test.com");

            client.Track("AnyEventName", user, "AnyJson");

            eventStore.Verify(s => s.Add(It.IsAny<CustomEvent>()));
            client.Dispose();
        }
    }
}
