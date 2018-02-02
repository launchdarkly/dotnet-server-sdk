using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using LaunchDarkly.Client;

namespace LaunchDarkly.Tests
{
    public class VariationOrRolloutTest
    {
        [Fact]
        public void TestBucketUserByKey()
        {
            var user1 = new User("userKeyA");
            var bucket = VariationOrRollout.BucketUser(user1, "hashKey", "key", "saltyA");
            Assert.InRange(bucket, 0.42157586, 0.42157588);

            var user2 = new User("userKeyB");
            bucket = VariationOrRollout.BucketUser(user2, "hashKey", "key", "saltyA");
            Assert.InRange(bucket, 0.6708484, 0.6708486);

            var user3 = new User("userKeyC");
            bucket = VariationOrRollout.BucketUser(user3, "hashKey", "key", "saltyA");
            Assert.InRange(bucket, 0.1034310, 0.1034311);
        }

        [Fact]
        public void TestBucketUserByIntAttr()
        {
            var user = new User("userKey").AndCustomAttribute("intAttr", 3);
            var bucket = VariationOrRollout.BucketUser(user, "hashKey", "intAttr", "saltyA");
            Assert.InRange(bucket, 0.0073090, 0.0073091);
        }

        [Fact]
        public void TestBucketUserByFloatAttr()
        {
            var user = new User("userKey").AndCustomAttribute("floatAttr", 999.999F);
            var bucket = VariationOrRollout.BucketUser(user, "hashKey", "floatAttr", "saltyA");
            Assert.Equal(0, bucket, 15);
        }
    }
}
