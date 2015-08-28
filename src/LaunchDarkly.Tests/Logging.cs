
using NUnit.Framework;
using LaunchDarkly.Client.Logging;
using NLog;
using NLog.Targets;
using LaunchDarkly.Client;
using RichardSzalay.MockHttp;
using System.Net;
using Moq;
using System.Net.Http;
using System;

namespace LaunchDarkly.Tests
{
    public class Logging
    {
        private string feature_json = "{\"name\":\"New dashboard enable\",\"key\":\"new.dashboard.enable\",\"kind\":\"flag\",\"salt\":\"ZW5hYmxlLnRlYW0uc2lnbnVw\",\"on\":true,\"variations\":[{\"value\":true,\"weight\":0,\"targets\":[{\"attribute\":\"key\",\"op\":\"in\",\"values\":[\"user@test.com\"]}],\"userTarget\":{\"attribute\":\"key\",\"op\":\"in\",\"values\":[\"user@test.com\"]}},{\"value\":false,\"weight\":100,\"targets\":[{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}],\"userTarget\":{\"attribute\":\"key\",\"op\":\"in\",\"values\":[]}}],\"ttl\":0,\"commitDate\":\"2015-05-14T20:54:58.713Z\",\"creationDate\":\"2015-05-08T20:11:55.732Z\"}";

        private MemoryTarget CreateInMemoryTarget(NLog.LogLevel logLevel)
        {
            var target = new MemoryTarget { Layout = "${message}" };
            NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(target, logLevel);

            return target;
        }

    }
}
