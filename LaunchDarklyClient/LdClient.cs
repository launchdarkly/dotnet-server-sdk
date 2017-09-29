using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using LaunchDarklyClient.Events;
using LaunchDarklyClient.Interfaces;
using Newtonsoft.Json.Linq;

namespace LaunchDarklyClient
{
	public class LdClient : IDisposable, ILdClient
	{
		private static readonly ILog log = LogManager.GetLogger<LdClient>();

		private readonly Configuration configuration;
		private readonly IStoreEvents eventStore;
		private readonly IFeatureStore featureStore;
		private readonly IUpdateProcessor updateProcessor;

		public LdClient(Configuration config, IStoreEvents eventStore)
		{
			try
			{
				log.Trace($"Start constructor {nameof(LdClient)}(Configuration, IStoreEvents)");

				log.Info($"Starting LaunchDarkly Client {Configuration.Version}");
				configuration = config;
				this.eventStore = eventStore;
				featureStore = config.FeatureStore;

				if (configuration.Offline)
				{
					log.Info("Starting Launchdarkly client in offline mode.");
					return;
				}

				FeatureRequestor featureRequestor = new FeatureRequestor(config);
				updateProcessor = new PollingProcessor(config, featureRequestor, featureStore);
				TaskCompletionSource<bool> initTask = updateProcessor.Start();
				log.Info($"Waiting up to {configuration.StartWaitTime.TotalMilliseconds} milliseconds for LaunchDarkly client to start..");
				initTask.Task.Wait(configuration.StartWaitTime);
			}
			finally
			{
				log.Trace($"End constructor {nameof(LdClient)}(Configuration, IStoreEvents)");
			}
		}

		public LdClient(Configuration config) : this(config, new EventProcessor(config))
		{
			try
			{
				log.Trace($"Start constructor {nameof(LdClient)}(Configuration)");
			}
			finally
			{
				log.Trace($"End constructor {nameof(LdClient)}(Configuration)");
			}
		}

		public LdClient(string sdkKey) : this(Configuration.Default(sdkKey))
		{
			try
			{
				log.Trace($"Start constructor {nameof(LdClient)}(string)");
			}
			finally
			{
				log.Trace($"End constructor {nameof(LdClient)}(string)");
			}
		}

		public void Dispose()
		{
			try
			{
				log.Trace($"Start {nameof(Dispose)}");

				Dispose(true);

				GC.SuppressFinalize(this);
			}
			finally
			{
				log.Trace($"End {nameof(Dispose)}");
			}
		}

		public bool Initialized()
		{
			try
			{
				log.Trace($"Start {nameof(Initialized)}");

				return IsOffline() || updateProcessor.Initialized();
			}
			finally
			{
				log.Trace($"End {nameof(Initialized)}");
			}
		}

		[Obsolete("Please use BoolVariation instead.")]
		public bool Toggle(string key, User user, bool defaultValue = false)
		{
			try
			{
				log.Trace($"Start {nameof(Toggle)}");

				log.Warn("Toggle() method is deprecated. Please use BoolVariation() instead");
				return BoolVariation(key, user, defaultValue);
			}
			finally
			{
				log.Trace($"End {nameof(Toggle)}");
			}
		}

		public bool BoolVariation(string key, User user, bool defaultValue = false)
		{
			try
			{
				log.Trace($"Start {nameof(BoolVariation)}");

				JToken value = Evaluate(key, user, defaultValue, JTokenType.Boolean);
				return value.Value<bool>();
			}
			finally
			{
				log.Trace($"End {nameof(BoolVariation)}");
			}
		}

		public int IntVariation(string key, User user, int defaultValue)
		{
			try
			{
				log.Trace($"Start {nameof(IntVariation)}");

				JToken value = Evaluate(key, user, defaultValue, JTokenType.Integer);
				return value.Value<int>();
			}
			finally
			{
				log.Trace($"End {nameof(IntVariation)}");
			}
		}

		public float FloatVariation(string key, User user, float defaultValue)
		{
			try
			{
				log.Trace($"Start {nameof(FloatVariation)}");

				JToken value = Evaluate(key, user, defaultValue, JTokenType.Float);
				return value.Value<float>();
			}
			finally
			{
				log.Trace($"End {nameof(FloatVariation)}");
			}
		}

		public string StringVariation(string key, User user, string defaultValue)
		{
			try
			{
				log.Trace($"Start {nameof(StringVariation)}");

				JToken value = Evaluate(key, user, defaultValue, JTokenType.String);
				return value.Value<string>();
			}
			finally
			{
				log.Trace($"End {nameof(StringVariation)}");
			}
		}

		public JToken JsonVariation(string key, User user, JToken defaultValue)
		{
			try
			{
				log.Trace($"Start {nameof(JsonVariation)}");

				JToken value = Evaluate(key, user, defaultValue, null);
				return value;
			}
			finally
			{
				log.Trace($"End {nameof(JsonVariation)}");
			}
		}

		public IDictionary<string, JToken> AllFlags(User user)
		{
			try
			{
				log.Trace($"Start {nameof(AllFlags)}");

				if (IsOffline())
				{
					log.Warn("AllFlags() was called when client is in offline mode. Returning null.");
					return null;
				}
				if (!Initialized())
				{
					log.Warn("AllFlags() was called before client has finished initializing. Returning null.");
					return null;
				}
				if (user == null || user.Key == null)
				{
					log.Warn("AllFlags() called with null user or null user key. Returning null");
					return null;
				}

				IDictionary<string, FeatureFlag> flags = featureStore.All();
				IDictionary<string, JToken> results = new Dictionary<string, JToken>();
				foreach (KeyValuePair<string, FeatureFlag> pair in flags)
				{
					try
					{
						FeatureFlag.EvalResult evalResult = pair.Value.Evaluate(user, featureStore);
						results.Add(pair.Key, evalResult.Result);
					}
					catch (Exception e)
					{
						log.Error("Exception caught when evaluating all flags: " + e.Message, e);
					}
				}
				return results;
			}
			finally
			{
				log.Trace($"End {nameof(AllFlags)}");
			}
		}

		public string SecureModeHash(User user)
		{
			try
			{
				log.Trace($"Start {nameof(SecureModeHash)}");

				if (user == null || string.IsNullOrEmpty(user.Key))
				{
					return null;
				}
				UTF8Encoding encoding = new UTF8Encoding();
				byte[] keyBytes = encoding.GetBytes(configuration.SdkKey);

				HMACSHA256 hmacSha256 = new HMACSHA256(keyBytes);
				byte[] hashedMessage = hmacSha256.ComputeHash(encoding.GetBytes(user.Key));
				return BitConverter.ToString(hashedMessage).Replace("-", "").ToLower();
			}
			finally
			{
				log.Trace($"End {nameof(SecureModeHash)}");
			}
		}

		public void Track(string name, User user, string data)
		{
			try
			{
				log.Trace($"Start {nameof(Track)}");

				if (user == null || user.Key == null)
				{
					log.Warn("Track called with null user or null user key");
				}
				eventStore.Add(new CustomEvent(name, user, data));
			}
			finally
			{
				log.Trace($"End {nameof(Track)}");
			}
		}

		public void Identify(User user)
		{
			try
			{
				log.Trace($"Start {nameof(Identify)}");

				if (user == null || user.Key == null)
				{
					log.Warn("Identify called with null user or null user key");
				}
				eventStore.Add(new IdentifyEvent(user));
			}
			finally
			{
				log.Trace($"End {nameof(Identify)}");
			}
		}

		public void Flush()
		{
			try
			{
				log.Trace($"Start {nameof(Flush)}");

				eventStore.Flush();
			}
			finally
			{
				log.Trace($"End {nameof(Flush)}");
			}
		}

		public bool IsOffline()
		{
			try
			{
				log.Trace($"Start {nameof(IsOffline)}");

				return configuration.Offline;
			}
			finally
			{
				log.Trace($"End {nameof(IsOffline)}");
			}
		}

		private JToken Evaluate(string featureKey, User user, JToken defaultValue, JTokenType? expectedType)
		{
			try
			{
				log.Trace($"Start {nameof(Evaluate)}");

				if (!Initialized())
				{
					log.Warn("LaunchDarkly client has not yet been initialized. Returning default");
					return defaultValue;
				}
				if (user == null || user.Key == null)
				{
					log.Warn("Feature flag evaluation called with null user or null user key. Returning default");
					SendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, null);
					return defaultValue;
				}

				try
				{
					FeatureFlag featureFlag = featureStore.Get(featureKey);
					if (featureFlag == null)
					{
						log.Warn("Unknown feature flag " + featureKey + "; returning default value: ");
						SendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, null);
						return defaultValue;
					}

					FeatureFlag.EvalResult evalResult = featureFlag.Evaluate(user, featureStore);
					if (!IsOffline())
					{
						foreach (FeatureRequestEvent prereqEvent in evalResult.PrerequisiteEvents)
						{
							eventStore.Add(prereqEvent);
						}
					}
					if (evalResult.Result != null)
					{
						if (expectedType != null && !evalResult.Result.Type.Equals(expectedType))
						{
							log.Error("Expected type: " + expectedType + " but got " + evalResult.GetType() +
							          " when evaluating FeatureFlag: " + featureKey + ". Returning default");
							SendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, featureFlag.Version);
							return defaultValue;
						}
						SendFlagRequestEvent(featureKey, user, evalResult.Result, defaultValue, featureFlag.Version);
						return evalResult.Result;
					}
				}
				catch (Exception e)
				{
					log.Error($"Encountered exception in LaunchDarkly client: {e.Message} when evaluating feature key: {featureKey} for user key: {user.Key}");
					log.Debug(e.ToString());
				}
				SendFlagRequestEvent(featureKey, user, defaultValue, defaultValue, null);
				return defaultValue;
			}
			finally
			{
				log.Trace($"End {nameof(Evaluate)}");
			}
		}

		private void SendFlagRequestEvent(string key, User user, JToken value, JToken defaultValue, JToken version)
		{
			try
			{
				log.Trace($"Start {nameof(SendFlagRequestEvent)}");

				eventStore.Add(new FeatureRequestEvent(key, user, value, defaultValue, version, null));
			}
			finally
			{
				log.Trace($"End {nameof(SendFlagRequestEvent)}");
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			try
			{
				log.Trace($"Start {nameof(Dispose)}");

				log.Info("Closing LaunchDarkly client.");
				//We do not have native resource, so the boolean parameter can be ignored.
				if (eventStore is EventProcessor)
				{
					(eventStore as IDisposable).Dispose();
				}

				if (updateProcessor != null)
				{
					updateProcessor.Dispose();
				}
			}
			finally
			{
				log.Trace($"End {nameof(Dispose)}");
			}
		}
	}
}