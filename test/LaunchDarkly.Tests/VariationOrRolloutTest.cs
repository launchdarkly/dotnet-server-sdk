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
            Assert.Equal(0.42157587, bucket, 6);

            var user2 = new User("userKeyB");
            bucket = VariationOrRollout.BucketUser(user2, "hashKey", "key", "saltyA");
            Assert.Equal(0.6708485, bucket, 6);

            var user3 = new User("userKeyC");
            bucket = VariationOrRollout.BucketUser(user3, "hashKey", "key", "saltyA");
            Assert.Equal(0.10343106, bucket, 6);
        }

        [Fact]
        public void TestBucketUserByIntAttr()
        {
            var user = new User("userKey").AndCustomAttribute("intAttr", 33333);
            var bucket = VariationOrRollout.BucketUser(user, "hashKey", "intAttr", "saltyA");
            Assert.Equal(0.54771423, bucket, 7);

            user = new User("userKey").AndCustomAttribute("stringAttr", "33333");
            var bucket2 = VariationOrRollout.BucketUser(user, "hashKey", "stringAttr", "saltyA");
            Assert.Equal(bucket, bucket2, 15);
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
