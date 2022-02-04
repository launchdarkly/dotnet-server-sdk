using System;
using System.Collections.Generic;
using LaunchDarkly.TestHelpers;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    public class DataModelDependenciesTest
    {
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
