using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.TestHelpers.HttpTest;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server
{
    public static class MockResponses
    {
        public static Handler Error401Response => Handlers.Status(401);

        public static Handler Error503Response => Handlers.Status(503);

        public static Handler EventsAcceptedResponse => Handlers.Status(202);

        public static Handler PollingResponse(FullDataSet<ItemDescriptor>? data = null) =>
            Handlers.BodyJson((data ?? DataSetBuilder.Empty).ToJsonString());

        public static Handler StreamWithInitialData(FullDataSet<ItemDescriptor>? data = null) =>
            Handlers.SSE.Start()
                .Then(PutEvent(data))
                .Then(Handlers.SSE.LeaveOpen());

        public static Handler PutEvent(FullDataSet<ItemDescriptor>? data = null) =>
            Handlers.SSE.Event(
                "put",
                @"{""data"":" + (data ?? DataSetBuilder.Empty).ToJsonString() + "}"
                );
    }
}
