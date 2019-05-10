using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using LaunchDarkly.Client;

namespace LaunchDarkly.Tests
{
    public class InMemoryFeatureStoreTest : FeatureStoreTestBase
    {
        public InMemoryFeatureStoreTest()
        {
            store = new InMemoryFeatureStore();
        }
    }
}
