using System;
using System.Collections.Generic;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class EventProcessorBuilderTest
    {
        private readonly BuilderInternalTestUtil<EventProcessorBuilder> _tester =
            BuilderTestUtil.For(Components.SendEvents);

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
            var prop = _tester.Property(b => b._baseUri, (b, v) => b.BaseUri(v));
            prop.AssertDefault(EventProcessorBuilder.DefaultBaseUri);
            prop.AssertCanSet(new Uri("http://x"));
            prop.AssertSetIsChangedTo(null, EventProcessorBuilder.DefaultBaseUri);
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
            b.PrivateAttributes("name");
            b.PrivateAttributes("email", "country");
            Assert.Equal(new HashSet<string> { "name", "email", "country" }, b._privateAttributes);
        }

        [Fact]
        public void UserKeysCapacity()
        {
            var prop = _tester.Property(b => b._userKeysCapacity, (b, v) => b.UserKeysCapacity(v));
            prop.AssertDefault(EventProcessorBuilder.DefaultUserKeysCapacity);
            prop.AssertCanSet(1);
        }

        [Fact]
        public void UserKeysFlushInterval()
        {
            var prop = _tester.Property(b => b._userKeysFlushInterval, (b, v) => b.UserKeysFlushInterval(v));
            prop.AssertDefault(EventProcessorBuilder.DefaultUserKeysFlushInterval);
            prop.AssertCanSet(TimeSpan.FromMinutes(7));
        }
    }
}
