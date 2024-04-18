﻿using System;
using LaunchDarkly.Sdk.Server.Subsystems;
using LaunchDarkly.TestHelpers;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class BigSegmentsConfigurationBuilderTest : BaseTest
    {
        private readonly IBigSegmentStore _store;
        private readonly IComponentConfigurer<IBigSegmentStore> _storeFactory;
        private readonly BuilderBehavior.InternalStateTester<BigSegmentsConfigurationBuilder> _tester;

        public BigSegmentsConfigurationBuilderTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            var storeMock = new Mock<IBigSegmentStore>();
            _store = storeMock.Object;
            var storeFactoryMock = new Mock<IComponentConfigurer<IBigSegmentStore>>();
            _storeFactory = storeFactoryMock.Object;
            storeFactoryMock.Setup(f => f.Build(BasicContext)).Returns(_store);

            _tester = BuilderBehavior.For(() => Components.BigSegments(_storeFactory));
        }

        [Fact]
        public void Store()
        {
            Assert.Same(_store, Components.BigSegments(_storeFactory).Build(BasicContext).Store);
        }

        [Fact]
        public void ContextCacheSize()
        {
            var prop = _tester.Property(b => b.Build(BasicContext).ContextCacheSize,
                (b, v) => b.ContextCacheSize(v));
            prop.AssertDefault(BigSegmentsConfigurationBuilder.DefaultContextCacheSize);
            prop.AssertCanSet(3333);
        }

        [Fact]
        public void ContextCacheTime()
        {
            var prop = _tester.Property(b => b.Build(BasicContext).ContextCacheTime,
                (b, v) => b.ContextCacheTime(v));
            prop.AssertDefault(BigSegmentsConfigurationBuilder.DefaultContextCacheTime);
            prop.AssertCanSet(TimeSpan.FromMilliseconds(3333));
        }

        [Fact]
        public void StatusPollInterval()
        {
            var prop = _tester.Property(b => b.Build(BasicContext).StatusPollInterval,
                (b, v) => b.StatusPollInterval(v));
            prop.AssertDefault(BigSegmentsConfigurationBuilder.DefaultStatusPollInterval);
            prop.AssertCanSet(TimeSpan.FromMilliseconds(3333));
            prop.AssertSetIsChangedTo(TimeSpan.Zero, BigSegmentsConfigurationBuilder.DefaultStatusPollInterval);
            prop.AssertSetIsChangedTo(TimeSpan.FromMilliseconds(-1), BigSegmentsConfigurationBuilder.DefaultStatusPollInterval);
        }

        [Fact]
        public void StaleAfter()
        {
            var prop = _tester.Property(b => b.Build(BasicContext).StaleAfter,
                (b, v) => b.StaleAfter(v));
            prop.AssertDefault(BigSegmentsConfigurationBuilder.DefaultStaleAfter);
            prop.AssertCanSet(TimeSpan.FromMilliseconds(3333));
            prop.AssertSetIsChangedTo(TimeSpan.Zero, BigSegmentsConfigurationBuilder.DefaultStaleAfter);
            prop.AssertSetIsChangedTo(TimeSpan.FromMilliseconds(-1), BigSegmentsConfigurationBuilder.DefaultStaleAfter);
        }
    }
}
