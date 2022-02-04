using System;
using System.IO;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;

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

        public static Handler EmptyPollingResponse => PollingResponse(null);

        public static Handler StreamWithEmptyData => StreamWithInitialData(null);

        public static Handler StreamWithInitialData(FullDataSet<ItemDescriptor>? data = null) =>
            Handlers.SSE.Start()
                .Then(PutEvent(data))
                .Then(Handlers.SSE.LeaveOpen());

        public static Handler StreamWithEmptyInitialDataAndThen(params Handler[] handlers)
        {
            var ret = Handlers.SSE.Start().Then(PutEvent());
            foreach (var h in handlers)
            {
                if (h != null)
                {
                    ret = ret.Then(h);
                }
            }
            return ret.Then(Handlers.SSE.LeaveOpen());
        }

        public static Handler StreamThatStaysOpenWithNoEvents =>
            Handlers.SSE.Start().Then(Handlers.SSE.LeaveOpen());

        public static Handler PutEvent(FullDataSet<ItemDescriptor>? data = null) =>
            Handlers.SSE.Event(
                "put",
                @"{""data"":" + (data ?? DataSetBuilder.Empty).ToJsonString() + "}"
                );

        public static Handler PatchEvent(string path, string data) =>
            Handlers.SSE.Event("patch", @"{""path"":""" + path + @""",""data"":" + data + "}");

        public static Handler DeleteEvent(string path, int version) =>
            Handlers.SSE.Event("delete", @"{""path"":""" + path + @""",""version"":" + version + "}");
    }
}
