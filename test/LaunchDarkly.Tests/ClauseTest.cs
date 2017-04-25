using LaunchDarkly.Client;
using Newtonsoft.Json;
using System.Collections.Generic;
using Xunit;

namespace LaunchDarkly.Tests {

    public class ClauseTest
    {

        private readonly Configuration _configuration = Configuration.Default("sdk-test");

        [Fact]
        public void DeserializeClauseFromJson()
        {
            Clause expectedClause = new Clause(
                attribute: "released",
                op: "after",
                values: new List<object> { 1491253200000.0 },
                negate: false
            );
            string json =
                "{ \"attribute\" : \"released\", \"negate\" : false, \"op\" : \"after\", \"values\" : [1491253200000.0] }";

            Clause actualClause = JsonConvert.DeserializeObject<Clause>(json);

            Assert.Equal(expectedClause.Attribute, actualClause.Attribute);
            Assert.Equal(expectedClause.Op, actualClause.Op);
            Assert.Equal(expectedClause.Negate, actualClause.Negate);
            Assert.Equal(expectedClause.Values, actualClause.Values);
        }

        [Theory]
        [MemberData(nameof(MatchesUser_UserWithPrimitiveAttributes_ReturnsTrueSource))]
        public void MatchesUser_UserWithPrimitiveAttributes_ReturnsTrue(
            string attribute,
            object userValue,
            object clauseValue)
        {
            Clause sut = new Clause(
                attribute: attribute,
                op: "in",
                values: new List<object> { clauseValue },
                negate: false
            );

            User user = User.WithKey("test-key")
                .AndCustomAttribute(attribute, userValue);

            bool result = sut.MatchesUser(user, _configuration);

            Assert.True(result);
        }

        public static IEnumerable<object[]> MatchesUser_UserWithPrimitiveAttributes_ReturnsTrueSource()
        {
            yield return new object[] { "profileUri", "www.site.com/profile?id=189", "www.site.com/profile?id=189" };
            yield return new object[] { "profileId", 189, 189 };
            yield return new object[] { "someInts", new[] { 78, 90 }, 90 };
        }

        [Theory]
        [MemberData(nameof(MatchesUser_UserWithPrimitiveAttributes_ReturnsFalseSource))]
        public void MatchesUser_UserWithPrimitiveAttributes_ReturnsFalse(
            string attribute,
            object userValue,
            object clauseValue)
        {
            Clause sut = new Clause(
                attribute: attribute,
                op: "in",
                values: new List<object> { clauseValue },
                negate: false
            );

            User user = User.WithKey("test-key")
                .AndCustomAttribute(attribute, userValue);

            bool result = sut.MatchesUser(user, _configuration);

            Assert.False(result);
        }

        public static IEnumerable<object[]> MatchesUser_UserWithPrimitiveAttributes_ReturnsFalseSource()
        {
            yield return new object[] { "profileUri", "www.site.com/profile?id=896", "www.site.com/profile?id=657" };
            yield return new object[] { "profileId", 192, 178 };
            yield return new object[] { "someInts", new[] { 456, 345 }, 784 };
        }

        [Fact]
        public void MatchesUser_WhenMultipleClauseValues_ReturnsTrue()
        {
            Clause sut = new Clause(
                attribute: "age",
                op: "in",
                values: new List<object> { 35, 40 },
                negate: false
            );

            User user = User.WithKey("test-key")
                .AndCustomAttribute("age", 40);

            bool result = sut.MatchesUser(user, _configuration);

            Assert.True(result);
        }

        [Fact]
        public void MatchesUser_WhenMultipleClauseValues_ReturnsFalse()
        {
            Clause sut = new Clause(
                attribute: "age",
                op: "in",
                values: new List<object> { 36, 41 },
                negate: false
            );

            User user = User.WithKey("test-key")
                .AndCustomAttribute("age", 40);

            bool result = sut.MatchesUser(user, _configuration);

            Assert.False(result);
        }

    }

}