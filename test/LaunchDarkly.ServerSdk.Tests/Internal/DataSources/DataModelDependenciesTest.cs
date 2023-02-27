using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using LaunchDarkly.Sdk.Json;
using LaunchDarkly.TestHelpers;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class DataModelDependenciesTest
    {
        [Theory]
        [InlineData("fr-FR")]
        [InlineData("de")]
        [InlineData("en-US")]
        public void SerializeUserIsInvariantToCulture(string cultureName)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
            System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureName);
            var user = User.Builder("user-key").Custom("doubleValue", 0.5).Build();
            // Will serialize an LDValue. Which would also be the same type for flag values.
            var serialized = LdJsonSerialization.SerializeObject(user);
            Assert.Matches(new Regex(".*{\"doubleValue\":0\\.5}.*"), serialized);
        }


        [Fact]
        public void KindAndKeyEqualityAndHashCodeTest()
        {
            // TypeBehavior.CheckEqualsAndHashCode verifies that each of these factories produces
            // instances that are equal to each other in terms of Equal and GetHashCode, and unequal
            // to the instances produced by any other factory.
            var factories = new List<Func<KindAndKey>>();
            foreach (var kind in new DataKind[] { DataModel.Features, DataModel.Segments })
            {
                foreach (var key in new string[] { "a", "b" })
                {
                    factories.Add(() => new KindAndKey(kind, key));
                }
            }
            TypeBehavior.CheckEqualsAndHashCode<KindAndKey>(factories.ToArray());
        }
    }
}
