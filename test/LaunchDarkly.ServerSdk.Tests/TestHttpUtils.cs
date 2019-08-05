using System;
using System.Net.Http;
using System.Threading.Tasks;
using WireMock.Server;
using WireMock.Settings;

namespace LaunchDarkly.Tests
{
    internal static class TestHttpUtils
    {
        internal static async Task<FluentMockServer> StartServerAsync()
        {
            var settings = new FluentMockServerSettings() { StartTimeout = 2000 };
            var server = FluentMockServer.Start(settings);
            // We've sometimes seen some instability in FluentMockServer that causes the server to not really be
            // ready when Start() returns, so we'll poll it to make sure.
            int nAttempts = 0;
            using (var client = new HttpClient())
            {
                while (true)
                {
                    try
                    {
                        await client.GetAsync(server.Urls[0]);
                        break;
                    }
                    catch (HttpRequestException)
                    {
                        if (nAttempts++ > 10)
                        {
                            throw new Exception("Test HTTP server did not become available within a reasonable time");
                        }
                        await Task.Delay(50);
                    }
                }
            }
            server.ResetLogEntries();
            return server;
        }
    }
}
