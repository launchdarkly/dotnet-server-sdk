using System;
using System.Collections.Generic;
using LaunchDarkly.TestHelpers;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class EventProcessorBuilderTest
    {
        private readonly BuilderBehavior.InternalStateTester<EventProcessorBuilder> _tester =
            BuilderBehavior.For(Components.SendEvents);

        [Fact]
        public void AllAttributesPrivate()
        {
            var prop = _tester.Property(b => b._allAttributesPrivate, (b, v) => b.AllAttributesPrivate(v));
            prop.AssertDefault(false);
            prop.AssertCanSet(true);
        }

        [Fact]
        public void BaseUri()
        {
#pragma warning disable CS0618
            var prop = _tester.Property(b => b._baseUri, (b, v) => b.BaseUri(v));
#pragma warning restore CS0618
            prop.AssertDefault(null);
            prop.AssertCanSet(new Uri("http://x"));
        }

        [Fact]
        public void Capacity()
        {
            var prop = _tester.Property(b => b._capacity, (b, v) => b.Capacity(v));
            prop.AssertDefault(EventProcessorBuilder.DefaultCapacity);
            prop.AssertCanSet(1);
            prop.AssertSetIsChangedTo(0, EventProcessorBuilder.DefaultCapacity);
            prop.AssertSetIsChangedTo(-1, EventProcessorBuilder.DefaultCapacity);
        }

        [Fact]
        public void DiagnosticRecordingInterval()
        {
            var prop = _tester.Property(b => b._diagnosticRecordingInterval, (b, v) => b.DiagnosticRecordingInterval(v));
            prop.AssertDefault(EventProcessorBuilder.DefaultDiagnosticRecordingInterval);
            prop.AssertCanSet(TimeSpan.FromMinutes(44));
            prop.AssertSetIsChangedTo(
                EventProcessorBuilder.MinimumDiagnosticRecordingInterval.Subtract(TimeSpan.FromMilliseconds(1)),
                EventProcessorBuilder.MinimumDiagnosticRecordingInterval);
        }

        [Fact]
        public void FlushInterval()
        {
            var prop = _tester.Property(b => b._flushInterval, (b, v) => b.FlushInterval(v));
            prop.AssertDefault(EventProcessorBuilder.DefaultFlushInterval);
            prop.AssertCanSet(TimeSpan.FromMinutes(7));
            prop.AssertSetIsChangedTo(TimeSpan.Zero, EventProcessorBuilder.DefaultFlushInterval);
            prop.AssertSetIsChangedTo(TimeSpan.FromMilliseconds(-1), EventProcessorBuilder.DefaultFlushInterval);
        }

        [Fact]
        public void InlineUsersInEvents()
        {
            var prop = _tester.Property(b => b._inlineUsersInEvents, (b, v) => b.InlineUsersInEvents(v));
            prop.AssertDefault(false);
            prop.AssertCanSet(true);
        }

        [Fact]
        public void PrivateAttributes()
        {
            var b = _tester.New();
            Assert.Empty(b._privateAttributes);
            b.PrivateAttributes(UserAttribute.Name);
            b.PrivateAttributes(UserAttribute.Email, UserAttribute.ForName("other"));
            Assert.Equal(new HashSet<UserAttribute> {
                UserAttribute.Name, UserAttribute.Email, UserAttribute.ForName("other") }, b._privateAttributes);
        }

        [Fact]
        public void PrivateAttributeNames()
        {
            var b = _tester.New();
            Assert.Empty(b._privateAttributes);
            b.PrivateAttributeNames("name");
            b.PrivateAttributeNames("email", "other");
            Assert.Equal(new HashSet<UserAttribute> {
                UserAttribute.Name, UserAttribute.Email, UserAttribute.ForName("other") }, b._privateAttributes);
        }

        [Fact]
        public void UserKeysCapacity()
        {
            var prop = _tester.Property(b => b._userKeysCapacity, (b, v) => b.UserKeysCapacity(v));
            prop.AssertDefault(EventProcessorBuilder.DefaultUserKeysCapacity);
            prop.AssertCanSet(1);
            prop.AssertSetIsChangedTo(0, EventProcessorBuilder.DefaultUserKeysCapacity);
            prop.AssertSetIsChangedTo(-1, EventProcessorBuilder.DefaultUserKeysCapacity);
        }

        [Fact]
        public void UserKeysFlushInterval()
        {
            var prop = _tester.Property(b => b._userKeysFlushInterval, (b, v) => b.UserKeysFlushInterval(v));
            prop.AssertDefault(EventProcessorBuilder.DefaultUserKeysFlushInterval);
            prop.AssertCanSet(TimeSpan.FromMinutes(7));
            prop.AssertSetIsChangedTo(TimeSpan.Zero, EventProcessorBuilder.DefaultUserKeysFlushInterval);
            prop.AssertSetIsChangedTo(TimeSpan.FromMilliseconds(-1), EventProcessorBuilder.DefaultUserKeysFlushInterval);
        }
    }
}
