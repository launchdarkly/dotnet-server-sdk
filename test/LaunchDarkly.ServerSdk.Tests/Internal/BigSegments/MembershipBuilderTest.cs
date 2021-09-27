using LaunchDarkly.TestHelpers;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.BigSegments
{
    // The factory method that we're calling here is BigSegmentStoreTypes.NewMembershipFromSegmentRefs,
    // which is in the Interfaces namespace-- but the underlying implementation is MembershipBuilder,
    // in the Internal.BigSegments namespace.

    public class MembershipBuilderTest
    {
        [Fact]
        public void EmptyMembership()
        {
            var m0 = NewMembershipFromSegmentRefs(null, null);
            var m1 = NewMembershipFromSegmentRefs(new string[0], null);
            var m2 = NewMembershipFromSegmentRefs(null, new string[0]);

            Assert.Same(m0, m1);
            Assert.Same(m0, m2);
            TypeBehavior.AssertEqual(m0, m1);

            Assert.Null(m0.CheckMembership("arbitrary"));
        }

        [Fact]
        public void MembershipWithSingleIncludeOnly()
        {
            var m0 = NewMembershipFromSegmentRefs(new string[] { "key1" }, null);
            var m1 = NewMembershipFromSegmentRefs(new string[] { "key1" }, null);

            Assert.NotSame(m0, m1);
            TypeBehavior.AssertEqual(m0, m1);

            Assert.True(m0.CheckMembership("key1"));
            Assert.Null(m0.CheckMembership("key2"));

            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(null, new string[] { "key1" }));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(new string[] { "key2" }, null));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(null, null));
        }

        [Fact]
        public void MembershipWithMultipleIncludesOnly()
        {
            var m0 = NewMembershipFromSegmentRefs(new string[] { "key1", "key2" }, null);
            var m1 = NewMembershipFromSegmentRefs(new string[] { "key2", "key1" }, null);

            Assert.NotSame(m0, m1);
            TypeBehavior.AssertEqual(m0, m1);

            Assert.True(m0.CheckMembership("key1"));
            Assert.True(m0.CheckMembership("key2"));
            Assert.Null(m0.CheckMembership("key3"));

            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(
                new string[] { "key1", "key2" }, new string[] { "key3" }));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(
                new string[] { "key1", "key3" }, null));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(new string[] { "key1" }, null));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(null, null));
        }

        [Fact]
        public void MembershipWithSingleExcludeOnly()
        {
            var m0 = NewMembershipFromSegmentRefs(null, new string[] { "key1" });
            var m1 = NewMembershipFromSegmentRefs(null, new string[] { "key1" });

            Assert.NotSame(m0, m1);
            TypeBehavior.AssertEqual(m0, m1);

            Assert.False(m0.CheckMembership("key1"));
            Assert.Null(m0.CheckMembership("key2"));

            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(new string[] { "key1" }, null));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(null, new string[] { "key2" }));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(null, null));
        }

        [Fact]
        public void MembershipWithMultipleExcludesOnly()
        {
            var m0 = NewMembershipFromSegmentRefs(null, new string[] { "key1", "key2" });
            var m1 = NewMembershipFromSegmentRefs(null, new string[] { "key2", "key1" });

            Assert.NotSame(m0, m1);
            TypeBehavior.AssertEqual(m0, m1);

            Assert.False(m0.CheckMembership("key1"));
            Assert.False(m0.CheckMembership("key2"));
            Assert.Null(m0.CheckMembership("key3"));

            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(
                new string[] { "key3" }, new string[] { "key1", "key2" }));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(
                null, new string[] { "key1", "key3" }));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(null, new string[] { "key1" }));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(null, null));
        }

        [Fact]
        public void MembershipWithIncludesAndExcludes()
        {
            var m0 = NewMembershipFromSegmentRefs(
                new string[] { "key1", "key2" },
                new string[] { "key2", "key3" }
                );
            // key1 is included; key2 is included and excluded, therefore it's included; key3 is excluded

            var m1 = NewMembershipFromSegmentRefs(
                new string[] { "key2", "key1" },
                new string[] { "key3", "key2" }
                );
            Assert.NotSame(m0, m1);
            TypeBehavior.AssertEqual(m0, m0);

            Assert.True(m0.CheckMembership("key1"));
            Assert.True(m0.CheckMembership("key2"));
            Assert.False(m0.CheckMembership("key3"));
            Assert.Null(m0.CheckMembership("key4"));


            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(
                new string[] { "key1", "key2" }, new string[] { "key2", "key3", "key4" }));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(
                new string[] { "key1", "key2", "key3" }, new string[] { "key2", "key3" }));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(new string[] { "key1" }, null));
            TypeBehavior.AssertNotEqual(m0, NewMembershipFromSegmentRefs(null, null));
        }
    }
}
