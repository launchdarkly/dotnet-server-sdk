using System;
using System.Collections.Generic;
using System.Net.Http;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Json;
using LaunchDarkly.Sdk.Server;

namespace TestService
{
    public class SdkClientEntity
    {
        private static HttpClient _httpClient = new HttpClient();

        private readonly LdClient _client;
        private readonly Logger _log;

        public SdkClientEntity(
            SdkConfigParams sdkParams,
            ILogAdapter logAdapter,
            string tag
        )
        {
            _log = logAdapter.Logger(tag);
            Configuration config = BuildSdkConfig(sdkParams, logAdapter, tag);

            _client = new LdClient(config);
            if (!_client.Initialized && !sdkParams.InitCanFail)
            {
                _client.Dispose();
                throw new Exception("Client initialization failed");
            }
        }
        
        public void Close()
        {
            _client.Dispose();
            _log.Info("Test ended");
        }

        public bool DoCommand(CommandParams command, out object response)
        {
            _log.Info("Test harness sent command: {0}", command.Command);
            response = null;
            switch (command.Command)
            {
                case "evaluate":
                    response = DoEvaluate(command.Evaluate);
                    break;

                case "evaluateAll":
                    response = DoEvaluateAll(command.EvaluateAll);
                    break;

                case "identifyEvent":
                    _client.Identify(command.IdentifyEvent.User);
                    break;

                case "customEvent":
                    var custom = command.CustomEvent;
                    if (custom.MetricValue.HasValue)
                    {
                        _client.Track(custom.EventKey, custom.User, custom.Data, custom.MetricValue.Value);
                    }
                    else if (custom.OmitNullData && custom.Data.IsNull)
                    {
                        _client.Track(custom.EventKey, custom.User);
                    }
                    else
                    {
                        _client.Track(custom.EventKey, custom.User, custom.Data);
                    }
                    break;

                case "flushEvents":
                    _client.Flush();
                    break;

                case "getBigSegmentStoreStatus":
                    var status = _client.BigSegmentStoreStatusProvider.Status;
                    response = new GetBigSegmentStoreStatusResponse { Available = status.Available, Stale = status.Stale };
                    break;

                default:
                    return false;
            }
            return true;
        }

        private object DoEvaluate(EvaluateFlagParams p)
        {
            var resp = new EvaluateFlagResponse();
            switch (p.ValueType)
            {
                case "bool":
                    if (p.Detail)
                    {
                        var detail = _client.BoolVariationDetail(p.FlagKey, p.User, p.DefaultValue.AsBool);
                        resp.Value = LdValue.Of(detail.Value);
                        resp.VariationIndex = detail.VariationIndex;
                        resp.Reason = detail.Reason;
                    }
                    else
                    {
                        resp.Value = LdValue.Of(_client.BoolVariation(p.FlagKey, p.User, p.DefaultValue.AsBool));
                    }
                    break;

                case "int":
                    if (p.Detail)
                    {
                        var detail = _client.IntVariationDetail(p.FlagKey, p.User, p.DefaultValue.AsInt);
                        resp.Value = LdValue.Of(detail.Value);
                        resp.VariationIndex = detail.VariationIndex;
                        resp.Reason = detail.Reason;
                    }
                    else
                    {
                        resp.Value = LdValue.Of(_client.IntVariation(p.FlagKey, p.User, p.DefaultValue.AsInt));
                    }
                    break;

                case "double":
                    if (p.Detail)
                    {
                        var detail = _client.DoubleVariationDetail(p.FlagKey, p.User, p.DefaultValue.AsDouble);
                        resp.Value = LdValue.Of(detail.Value);
                        resp.VariationIndex = detail.VariationIndex;
                        resp.Reason = detail.Reason;
                    }
                    else
                    {
                        resp.Value = LdValue.Of(_client.DoubleVariation(p.FlagKey, p.User, p.DefaultValue.AsDouble));
                    }
                    break;

                case "string":
                    if (p.Detail)
                    {
                        var detail = _client.StringVariationDetail(p.FlagKey, p.User, p.DefaultValue.AsString);
                        resp.Value = LdValue.Of(detail.Value);
                        resp.VariationIndex = detail.VariationIndex;
                        resp.Reason = detail.Reason;
                    }
                    else
                    {
                        resp.Value = LdValue.Of(_client.StringVariation(p.FlagKey, p.User, p.DefaultValue.AsString));
                    }
                    break;

                default:
                    if (p.Detail)
                    {
                        var detail = _client.JsonVariationDetail(p.FlagKey, p.User, p.DefaultValue);
                        resp.Value = detail.Value;
                        resp.VariationIndex = detail.VariationIndex;
                        resp.Reason = detail.Reason;
                    }
                    else
                    {
                        resp.Value = _client.JsonVariation(p.FlagKey, p.User, p.DefaultValue);
                    }
                    break;
            }
            return resp;
        }

        private object DoEvaluateAll(EvaluateAllFlagsParams p)
        {
            var options = new List<FlagsStateOption>();
            if (p.ClientSideOnly)
            {
                options.Add(FlagsStateOption.ClientSideOnly);
            }
            if (p.DetailsOnlyForTrackedFlags)
            {
                options.Add(FlagsStateOption.DetailsOnlyForTrackedFlags);
            }
            if (p.WithReasons)
            {
                options.Add(FlagsStateOption.WithReasons);
            }
            var result = _client.AllFlagsState(p.User, options.ToArray());
            return new EvaluateAllFlagsResponse
            {
                State = LdValue.Parse(LdJsonSerialization.SerializeObject(result))
            };
        }

        private static Configuration BuildSdkConfig(SdkConfigParams sdkParams, ILogAdapter logAdapter, string tag)
        {
            var builder = Configuration.Builder(sdkParams.Credential);

            builder.Logging(Components.Logging(logAdapter).BaseLoggerName(tag + ".SDK"));
            var endpoints = Components.ServiceEndpoints();
            builder.ServiceEndpoints(endpoints);

            if (sdkParams.StartWaitTimeMs.HasValue)
            {
                builder.StartWaitTime(TimeSpan.FromMilliseconds(sdkParams.StartWaitTimeMs.Value));
            }

            var streamingParams = sdkParams.Streaming;
            if (streamingParams != null)
            {
                endpoints.Streaming(streamingParams.BaseUri);
                var dataSource = Components.StreamingDataSource();
                if (streamingParams.InitialRetryDelayMs.HasValue)
                {
                    dataSource.InitialReconnectDelay(TimeSpan.FromMilliseconds(streamingParams.InitialRetryDelayMs.Value));
                }
                builder.DataSource(dataSource);
            }

            var eventParams = sdkParams.Events;
            if (eventParams == null)
            {
                builder.Events(Components.NoEvents);
            }
            else
            {
                endpoints.Events(eventParams.BaseUri);
                var events = Components.SendEvents()
                    .AllAttributesPrivate(eventParams.AllAttributesPrivate);
                if (eventParams.Capacity.HasValue && eventParams.Capacity.Value > 0)
                {
                    events.Capacity(eventParams.Capacity.Value);
                }
                if (eventParams.FlushIntervalMs.HasValue && eventParams.FlushIntervalMs.Value > 0)
                {
                    events.FlushInterval(TimeSpan.FromMilliseconds(eventParams.FlushIntervalMs.Value));
                }
                if (eventParams.GlobalPrivateAttributes != null)
                {
                    events.PrivateAttributeNames(eventParams.GlobalPrivateAttributes);
                }
                builder.Events(events);
                builder.DiagnosticOptOut(!eventParams.EnableDiagnostics);
            }

            var bigSegments = sdkParams.BigSegments;
            if (bigSegments != null)
            {
                var bs = Components.BigSegments(new BigSegmentStoreFixture(new CallbackService(bigSegments.CallbackUri)));
                if (bigSegments.StaleAfterMs.HasValue)
                {
                    bs.StaleAfter(TimeSpan.FromMilliseconds(bigSegments.StaleAfterMs.Value));
                }
                if (bigSegments.StatusPollIntervalMs.HasValue)
                {
                    bs.StatusPollInterval(TimeSpan.FromMilliseconds(bigSegments.StatusPollIntervalMs.Value));
                }
                if (bigSegments.UserCacheSize.HasValue)
                {
                    bs.UserCacheSize(bigSegments.UserCacheSize.Value);
                }
                if (bigSegments.UserCacheTimeMs.HasValue)
                {
                    bs.UserCacheTime(TimeSpan.FromMilliseconds(bigSegments.UserCacheTimeMs.Value));
                }
                builder.BigSegments(bs);
            }

            return builder.Build();
        }
    }
}
