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
        public void PrivateAttributes()
        {
            var b = _tester.New();
            Assert.Empty(b._privateAttributes);
            b.PrivateAttributes("email", "/address/street");
            Assert.Equal(new HashSet<AttributeRef> {
                AttributeRef.FromLiteral("email"), AttributeRef.FromPath("/address/street") },
                b._privateAttributes);
        }

        [Fact]
        public void ContextKeysCapacity()
        {
            var prop = _tester.Property(b => b._contextKeysCapacity, (b, v) => b.ContextKeysCapacity(v));
            prop.AssertDefault(EventProcessorBuilder.DefaultContextKeysCapacity);
            prop.AssertCanSet(1);
            prop.AssertSetIsChangedTo(0, EventProcessorBuilder.DefaultContextKeysCapacity);
            prop.AssertSetIsChangedTo(-1, EventProcessorBuilder.DefaultContextKeysCapacity);
        }

        [Fact]
        public void ContextKeysFlushInterval()
        {
            var prop = _tester.Property(b => b._contextKeysFlushInterval, (b, v) => b.ContextKeysFlushInterval(v));
            prop.AssertDefault(EventProcessorBuilder.DefaultContextKeysFlushInterval);
            prop.AssertCanSet(TimeSpan.FromMinutes(7));
            prop.AssertSetIsChangedTo(TimeSpan.Zero, EventProcessorBuilder.DefaultContextKeysFlushInterval);
            prop.AssertSetIsChangedTo(TimeSpan.FromMilliseconds(-1), EventProcessorBuilder.DefaultContextKeysFlushInterval);
        }
    }
}
