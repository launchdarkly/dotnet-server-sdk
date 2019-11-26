using Xunit;

namespace LaunchDarkly.Sdk.Server.Model
{
    public class BucketingTest
    {
        [Fact]
        public void TestBucketUserByKey()
        {
            var user1 = User.WithKey("userKeyA");
            var bucket = Bucketing.BucketUser(user1, "hashKey", "key", "saltyA");
            Assert.Equal(0.42157587, bucket, 6);

            var user2 = User.WithKey("userKeyB");
            bucket = Bucketing.BucketUser(user2, "hashKey", "key", "saltyA");
            Assert.Equal(0.6708485, bucket, 6);

            var user3 = User.WithKey("userKeyC");
            bucket = Bucketing.BucketUser(user3, "hashKey", "key", "saltyA");
            Assert.Equal(0.10343106, bucket, 6);
        }

        [Fact]
        public void TestBucketUserByIntAttr()
        {
            var user = User.Builder("userKey").Custom("intAttr", 33333).Build();
            var bucket = Bucketing.BucketUser(user, "hashKey", "intAttr", "saltyA");
            Assert.Equal(0.54771423, bucket, 7);

            user = User.Builder("userKey").Custom("stringAttr", "33333").Build();
            var bucket2 = Bucketing.BucketUser(user, "hashKey", "stringAttr", "saltyA");
            Assert.Equal(bucket, bucket2, 15);
        }

        [Fact]
        public void TestBucketUserByFloatAttr()
        {
            var user = User.Builder("userKey").Custom("floatAttr", 999.999F).Build();
            var bucket = Bucketing.BucketUser(user, "hashKey", "floatAttr", "saltyA");
            Assert.Equal(0, bucket, 15);
        }
    }
}
