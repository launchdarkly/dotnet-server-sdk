using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server;
using LaunchDarkly.TestHelpers.HttpTest;

namespace TestService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset);
            var app = new Webapp(quitSignal);
            var server = HttpServer.Start(8000, app.Handler);
            server.Recorder.Enabled = false;
            quitSignal.WaitOne();
            server.Dispose();
        }
    }

    public class Webapp
    {
        private static readonly string[] Capabilities = {
            "server-side",
            "strongly-typed",
            "all-flags-client-side-only",
            "all-flags-details-only-for-tracked-flags",
            "all-flags-with-reasons",
            "big-segments",
            "context-type",
            "secure-mode-hash",
            "user-type"
        };

        public readonly Handler Handler;

        private readonly string _version;
        private readonly ILogAdapter _logging = Logs.ToConsole;
        private readonly ConcurrentDictionary<string, SdkClientEntity> _clients =
            new ConcurrentDictionary<string, SdkClientEntity>();
        private readonly EventWaitHandle _quitSignal;
        private volatile int _lastClientId = 0;

        public Webapp(EventWaitHandle quitSignal)
        {
            _quitSignal = quitSignal;

            var dummyClientInstanceToGetVersion = new LdClient(Configuration.Builder("")
                .Offline(true).Logging(Components.NoLogging).Build());
            _version = dummyClientInstanceToGetVersion.Version.ToString();

            var service = new SimpleJsonService();
            Handler = service.Handler;

            service.Route(HttpMethod.Get, "/", GetStatus);
            service.Route(HttpMethod.Delete, "/", ForceQuit);
            service.Route<CreateInstanceParams>(HttpMethod.Post, "/", PostCreateClient);
            service.Route<CommandParams, object>(HttpMethod.Post, "/clients/(.*)", PostClientCommand);
            service.Route(HttpMethod.Delete, "/clients/(.*)", DeleteClient);
        }

        SimpleResponse<Status> GetStatus(IRequestContext context) =>
            SimpleResponse.Of(200, new Status
            {
                Name = "dotnet-server-sdk",
                Capabilities = Capabilities,
                ClientVersion = _version
            });

        SimpleResponse ForceQuit(IRequestContext context)
        {
            _logging.Logger("").Info("Test harness has told us to exit");

            // The web server won't send the response till we return, so we'll defer the actual shutdown
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                _quitSignal.Set();
            });

            return SimpleResponse.Of(204);
        }

        SimpleResponse PostCreateClient(IRequestContext context, CreateInstanceParams createParams)
        {
            var client = new SdkClientEntity(createParams.Configuration, _logging, createParams.Tag);

            var id = Interlocked.Increment(ref _lastClientId);
            var clientId = id.ToString();
            _clients[clientId] = client;

            var resourceUrl = "/clients/" + clientId;
            return SimpleResponse.Of(201).WithHeader("Location", resourceUrl);
        }

        SimpleResponse<object> PostClientCommand(IRequestContext context, CommandParams command)
        {
            var id = context.GetPathParam(0);
            if (!_clients.TryGetValue(id, out var client))
            {
                return SimpleResponse.Of<object>(404, null);
            }

            if (client.DoCommand(command, out var response))
            {
                return SimpleResponse.Of(202, response);
            }
            else
            {
                return SimpleResponse.Of<object>(400, null);
            }
        }

        SimpleResponse DeleteClient(IRequestContext context)
        {
            var id = context.GetPathParam(0);
            if (!_clients.TryGetValue(id, out var client))
            {
                return SimpleResponse.Of(400);
            }
            client.Close();
            _clients.TryRemove(id, out _);

            return SimpleResponse.Of(204);
        }
    }
}
