﻿using System;
using LaunchDarkly.TestHelpers;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class PollingDataSourceBuilderTest
    {
        private readonly BuilderBehavior.InternalStateTester<PollingDataSourceBuilder> _tester =
            BuilderBehavior.For(Components.PollingDataSource);

        [Fact]
        public void PollInterval()
        {
            var prop = _tester.Property(b => b._pollInterval, (b, v) => b.PollInterval(v));
            prop.AssertDefault(PollingDataSourceBuilder.DefaultPollInterval);
            prop.AssertCanSet(TimeSpan.FromMinutes(7));
            prop.AssertSetIsChangedTo(
                PollingDataSourceBuilder.DefaultPollInterval.Subtract(TimeSpan.FromMilliseconds(1)),
                PollingDataSourceBuilder.DefaultPollInterval);
        }
    }
}
