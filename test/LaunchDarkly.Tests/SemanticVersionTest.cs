using LaunchDarkly.Client;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class SemanticVersionTest
    {
        [Fact]
        public void CanParseSimpleCompleteVersion()
        {
            var sv = SemanticVersion.Parse("2.3.4");
            Assert.Equal(2, sv.Major);
            Assert.Equal(3, sv.Minor);
            Assert.Equal(4, sv.Patch);
            Assert.Equal("", sv.Prerelease);
            Assert.Equal("", sv.Build);
        }

        [Fact]
        public void CanParseVersionWithPrerelease()
        {
            var sv = SemanticVersion.Parse("2.3.4-beta1.rc2");
            Assert.Equal(2, sv.Major);
            Assert.Equal(3, sv.Minor);
            Assert.Equal(4, sv.Patch);
            Assert.Equal("beta1.rc2", sv.Prerelease);
            Assert.Equal("", sv.Build);
        }

        [Fact]
        public void CanParseVersionWithBuild()
        {
            var sv = SemanticVersion.Parse("2.3.4+build2.4");
            Assert.Equal(2, sv.Major);
            Assert.Equal(3, sv.Minor);
            Assert.Equal(4, sv.Patch);
            Assert.Equal("", sv.Prerelease);
            Assert.Equal("build2.4", sv.Build);
        }

        [Fact]
        public void CanParseVersionWithPrereleaseAndBuild()
        {
            var sv = SemanticVersion.Parse("2.3.4-beta1.rc2+build2.4");
            Assert.Equal(2, sv.Major);
            Assert.Equal(3, sv.Minor);
            Assert.Equal(4, sv.Patch);
            Assert.Equal("beta1.rc2", sv.Prerelease);
            Assert.Equal("build2.4", sv.Build);
        }

        [Fact]
        public void CanParseVersionWithMajorOnly()
        {
            var sv = SemanticVersion.Parse("2", true);
            Assert.Equal(2, sv.Major);
            Assert.Equal(0, sv.Minor);
            Assert.Equal(0, sv.Patch);
            Assert.Equal("", sv.Prerelease);
            Assert.Equal("", sv.Build);
        }

        [Fact]
        public void CannotParseVersionWithMajorOnlyIfFlagNotSet()
        {
            Assert.Throws<ArgumentException>(() => SemanticVersion.Parse("2", false));
        }

        [Fact]
        public void CanParseVersionWithMajorAndMinorOnly()
        {
            var sv = SemanticVersion.Parse("2.3", true);
            Assert.Equal(2, sv.Major);
            Assert.Equal(3, sv.Minor);
            Assert.Equal(0, sv.Patch);
            Assert.Equal("", sv.Prerelease);
            Assert.Equal("", sv.Build);
        }

        [Fact]
        public void CannotParseVersionWithMajorAndMinorOnlyIfFlagNotSet()
        {
            Assert.Throws<ArgumentException>(() => SemanticVersion.Parse("2.3", false));
        }

        [Fact]
        public void CanParseVersionWithMajorAndPrereleaseOnly()
        {
            var sv = SemanticVersion.Parse("2-beta1", true);
            Assert.Equal(2, sv.Major);
            Assert.Equal(0, sv.Minor);
            Assert.Equal(0, sv.Patch);
            Assert.Equal("beta1", sv.Prerelease);
            Assert.Equal("", sv.Build);
        }

        [Fact]
        public void CanParseVersionWithMajorMinorAndPrereleaseOnly()
        {
            var sv = SemanticVersion.Parse("2.3-beta1", true);
            Assert.Equal(2, sv.Major);
            Assert.Equal(3, sv.Minor);
            Assert.Equal(0, sv.Patch);
            Assert.Equal("beta1", sv.Prerelease);
            Assert.Equal("", sv.Build);
        }

        [Fact]
        public void CanParseVersionWithMajorAndBuildOnly()
        {
            var sv = SemanticVersion.Parse("2+build1", true);
            Assert.Equal(2, sv.Major);
            Assert.Equal(0, sv.Minor);
            Assert.Equal(0, sv.Patch);
            Assert.Equal("", sv.Prerelease);
            Assert.Equal("build1", sv.Build);
        }

        [Fact]
        public void CanParseVersionWithMajorMinorAndBuildOnly()
        {
            var sv = SemanticVersion.Parse("2.3+build1", true);
            Assert.Equal(2, sv.Major);
            Assert.Equal(3, sv.Minor);
            Assert.Equal(0, sv.Patch);
            Assert.Equal("", sv.Prerelease);
            Assert.Equal("build1", sv.Build);
        }

        [Fact]
        public void EqualVersionsHaveEqualPrecedence()
        {
            var sv1 = SemanticVersion.Parse("2.3.4-beta1");
            var sv2 = SemanticVersion.Parse("2.3.4-beta1");
            Assert.Equal(0, sv1.ComparePrecedence(sv2));
        }

        [Fact]
        public void LowerMajorVersionHasLowerPrecedence()
        {
            var sv1 = SemanticVersion.Parse("1.3.4-beta1");
            var sv2 = SemanticVersion.Parse("2.3.4-beta1");
            Assert.Equal(-1, sv1.ComparePrecedence(sv2));
            Assert.Equal(1, sv2.ComparePrecedence(sv1));
        }

        [Fact]
        public void LowerMinorVersionHasLowerPrecedence()
        {
            var sv1 = SemanticVersion.Parse("2.2.4-beta1");
            var sv2 = SemanticVersion.Parse("2.3.4-beta1");
            Assert.Equal(-1, sv1.ComparePrecedence(sv2));
            Assert.Equal(1, sv2.ComparePrecedence(sv1));
        }

        [Fact]
        public void LowerPatchVersionHasLowerPrecedence()
        {
            var sv1 = SemanticVersion.Parse("2.3.3-beta1");
            var sv2 = SemanticVersion.Parse("2.3.4-beta1");
            Assert.Equal(-1, sv1.ComparePrecedence(sv2));
            Assert.Equal(1, sv2.ComparePrecedence(sv1));
        }

        [Fact]
        public void PrereleaseVersionHasLowerPrecedenceThanRelease()
        {
            var sv1 = SemanticVersion.Parse("2.3.4-beta1");
            var sv2 = SemanticVersion.Parse("2.3.4");
            Assert.Equal(-1, sv1.ComparePrecedence(sv2));
            Assert.Equal(1, sv2.ComparePrecedence(sv1));
        }

        [Fact]
        public void ShorterSubsetOfPrereleaseIdentifiersHasLowerPrecedence()
        {
            var sv1 = SemanticVersion.Parse("2.3.4-beta1");
            var sv2 = SemanticVersion.Parse("2.3.4-beta1.rc1");
            Assert.Equal(-1, sv1.ComparePrecedence(sv2));
            Assert.Equal(1, sv2.ComparePrecedence(sv1));
        }

        [Fact]
        public void NumericPrereleaseIdentifiersAreSortedNumerically()
        {
            var sv1 = SemanticVersion.Parse("2.3.4-beta1.3");
            var sv2 = SemanticVersion.Parse("2.3.4-beta1.23");
            Assert.Equal(-1, sv1.ComparePrecedence(sv2));
            Assert.Equal(1, sv2.ComparePrecedence(sv1));
        }

        [Fact]
        public void NonNumericPrereleaseIdentifiersAreSortedAsStrings()
        {
            var sv1 = SemanticVersion.Parse("2.3.4-beta1.x3");
            var sv2 = SemanticVersion.Parse("2.3.4-beta1.x23");
            Assert.Equal(1, sv1.ComparePrecedence(sv2));
            Assert.Equal(-1, sv2.ComparePrecedence(sv1));
        }

        [Fact]
        public void BuildIdentifierDoesNotAffectPrecedence()
        {
            var sv1 = SemanticVersion.Parse("2.3.4-beta1+build1");
            var sv2 = SemanticVersion.Parse("2.3.4-beta1+build2");
            Assert.Equal(0, sv1.ComparePrecedence(sv2));
            Assert.Equal(0, sv2.ComparePrecedence(sv1));
        }
    }
}
