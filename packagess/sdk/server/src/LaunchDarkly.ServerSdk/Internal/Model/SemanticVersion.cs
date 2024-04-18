using System;
using System.Text.RegularExpressions;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal struct SemanticVersion
    {
        static readonly Regex VERSION_REGEX = new Regex(
            @"^(?<major>0|[1-9]\d*)(\.(?<minor>0|[1-9]\d*))?(\.(?<patch>0|[1-9]\d*))?" +
            @"(\-(?<prerel>[0-9A-Za-z\-\.]+))?(\+(?<build>[0-9A-Za-z\-\.]+))?$",
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }
        public String Prerelease { get; private set; }
        public String Build { get; private set; }

        public SemanticVersion(int major, int minor, int patch, String prerelease, String build)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease;
            Build = build;
        }

        /// <summary>
        /// Attempts to parse a string as a semantic version according to the Semver 2.0.0 specification, except that
        /// the minor and patch versions may optionally be omitted.
        /// </summary>
        /// <param name="s">the input string</param>
        /// <param name="allowMissingMinorAndPatch">true if the parser should tolerate the absence of a minor and/or
        /// patch version; if absent, they will be treated as zero</param>
        /// <returns></returns>
        public static SemanticVersion Parse(string s, bool allowMissingMinorAndPatch = false)
        {
            var m = VERSION_REGEX.Match(s);
            if (!m.Success)
            {
                throw new ArgumentException("Invalid semantic version");
            }
            var major = int.Parse(m.Groups["major"].Value);
            if ((!m.Groups["minor"].Success || !m.Groups["patch"].Success) && !allowMissingMinorAndPatch)
            {
                throw new ArgumentException("Invalid semantic version");
            }
            var minor = m.Groups["minor"].Success ? int.Parse(m.Groups["minor"].Value) : 0;
            var patch = m.Groups["patch"].Success ? int.Parse(m.Groups["patch"].Value) : 0;
            var prerelease = m.Groups["prerel"].Value;
            var build = m.Groups["build"].Value;
            return new SemanticVersion(major, minor, patch, prerelease, build);
        }

        /// <summary>
        /// Compares this object with another SemanticVersion according to Semver 2.0.0 precedence rules.
        /// </summary>
        /// <param name="other">another SemanticVersion</param>
        /// <returns>0 if equal, -1 if the current object has lower precedence, or 1 if the current object has higher precedence</returns>
        public int ComparePrecedence(SemanticVersion other)
        {
            if (Major != other.Major)
            {
                return Major.CompareTo(other.Major);
            }
            if (Minor != other.Minor)
            {
                return Minor.CompareTo(other.Minor);
            }
            if (Patch != other.Patch)
            {
                return Patch.CompareTo(other.Patch);
            }
            if (String.IsNullOrEmpty(Prerelease) && String.IsNullOrEmpty(other.Prerelease))
            {
                return 0; // build component is ignored in precedence comparison
            }
            // *no* prerelease component always has higher precedence than *any* prerelease component
            if (String.IsNullOrEmpty(Prerelease))
            {
                return 1;
            }
            if (String.IsNullOrEmpty(other.Prerelease))
            {
                return -1;
            }
            return CompareIdentifiers(Prerelease.Split('.'), other.Prerelease.Split('.'));
        }

        private int CompareIdentifiers(string[] ids1, string[] ids2)
        {
            for (int i = 0; ; i++)
            {
                if (i >= ids1.Length)
                {
                    // x.y is always less than x.y.z
                    return (i >= ids2.Length) ? 0 : -1;
                }
                if (i >= ids2.Length)
                {
                    return 1;
                }
                // each sub-identifier is compared numerically if both are numeric; if both are non-numeric,
                // they're compared as strings; otherwise, the numeric one is the lesser one
                int n1, n2, d;
                bool isNum1, isNum2;
                isNum1 = Int32.TryParse(ids1[i], out n1);
                isNum2 = Int32.TryParse(ids2[i], out n2);
                if (isNum1 && isNum2)
                {
                    d = n1.CompareTo(n2);
                }
                else
                {
                    d = isNum1 ? -1 : (isNum2 ? 1 : ids1[i].CompareTo(ids2[i]));
                }
                if (d != 0)
                {
                    return d;
                }
            }
        }
    }
}
