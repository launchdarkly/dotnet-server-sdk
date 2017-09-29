using System;
using System.Net.Http;
using LaunchDarklyClient.Extensions;
using NUnit.Framework;

namespace LaunchDarklyClient.Tests
{
	[TestFixture]
	public class ConfigurationTest
	{
		[Test]
		public void CannotOverrideTooSmallPollingInterval()
		{
			Configuration config = Configuration.Default("AnyOtherSdkKey")
				.WithPollingInterval(TimeSpan.FromMilliseconds(100));

			TimeSpan expected = TimeSpan.FromSeconds(1);
			Assert.AreEqual(expected, config.PollingInterval);
		}

		[Test]
		public void CanOverrideConfiguration()
		{
			Configuration config = Configuration.Default("AnyOtherSdkKey")
				.WithUri("https://app.AnyOtherEndpoint.com")
				.WithEventQueueCapacity(99)
				.WithPollingInterval(TimeSpan.FromSeconds(1.5));

			Assert.AreEqual(new Uri("https://app.AnyOtherEndpoint.com"), config.BaseUri);
			Assert.AreEqual("AnyOtherSdkKey", config.SdkKey);
			Assert.AreEqual(99, config.EventQueueCapacity);
			Assert.AreEqual(TimeSpan.FromSeconds(1.5), config.PollingInterval);
		}

		[Test]
		public void CanSetHttpClientHandler()
		{
			HttpClientHandler handler = new HttpClientHandler();
			Configuration config = Configuration.Default("AnyOtherSdkKey")
				.WithHttpClientHandler(handler);

			Assert.AreEqual(handler, config.HttpClientHandler);
		}
	}
}