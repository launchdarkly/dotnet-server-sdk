using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Moq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class ILdClientExtensionsTests
    {
        private static readonly User defaultUser = User.WithKey("userkey");

        enum MyEnum
        {
            Red,
            Green,
            Blue
        };

        [Fact]
        public void EnumVariationConvertsStringToEnum()
        {
            var clientMock = new Mock<ILdClient>();
            clientMock.Setup(c => c.StringVariation("key", defaultUser, "Blue")).Returns("Green");
            var client = clientMock.Object;

            var result = client.EnumVariation("key", defaultUser, MyEnum.Blue);
            Assert.Equal(MyEnum.Green, result);
        }
        
        [Fact]
        public void EnumVariationReturnsDefaultValueForInvalidFlagValue()
        {
            var clientMock = new Mock<ILdClient>();
            clientMock.Setup(c => c.StringVariation("key", defaultUser, "Blue")).Returns("not-a-color");
            var client = clientMock.Object;

            var defaultValue = MyEnum.Blue;
            var result = client.EnumVariation("key", defaultUser, defaultValue);
            Assert.Equal(MyEnum.Blue, defaultValue);
        }

        [Fact]
        public void EnumVariationReturnsDefaultValueForNullFlagValue()
        {
            var clientMock = new Mock<ILdClient>();
            clientMock.Setup(c => c.StringVariation("key", defaultUser, "Blue")).Returns((string)null);
            var client = clientMock.Object;

            var defaultValue = MyEnum.Blue;
            var result = client.EnumVariation("key", defaultUser, defaultValue);
            Assert.Equal(defaultValue, result);
        }

        [Fact]
        public void EnumVariationReturnsDefaultValueForNonEnumType()
        {
            var clientMock = new Mock<ILdClient>();
            clientMock.Setup(c => c.StringVariation("key", defaultUser, "Blue")).Returns("Green");
            var client = clientMock.Object;

            var defaultValue = "this is a string, not an enum";
            var result = client.EnumVariation("key", defaultUser, defaultValue);
            Assert.Equal(defaultValue, result);
        }

        [Fact]
        public void EnumVariationDetailConvertsStringToEnum()
        {
            var clientMock = new Mock<ILdClient>();
            clientMock.Setup(c => c.StringVariationDetail("key", defaultUser, "Blue"))
                .Returns(new EvaluationDetail<string>("Green", 1, EvaluationReason.FallthroughReason));
            var client = clientMock.Object;

            var result = client.EnumVariationDetail("key", defaultUser, MyEnum.Blue);
            var expected = new EvaluationDetail<MyEnum>(MyEnum.Green, 1, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EnumVariationDetailReturnsDefaultValueForInvalidFlagValue()
        {
            var clientMock = new Mock<ILdClient>();
            clientMock.Setup(c => c.StringVariationDetail("key", defaultUser, "Blue"))
                .Returns(new EvaluationDetail<string>("not-a-color", 1, EvaluationReason.FallthroughReason));
            var client = clientMock.Object;

            var result = client.EnumVariationDetail("key", defaultUser, MyEnum.Blue);
            var expected = new EvaluationDetail<MyEnum>(MyEnum.Blue, 1, EvaluationReason.ErrorReason(EvaluationErrorKind.WRONG_TYPE));
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EnumVariationDetailReturnsDefaultValueForNullFlagValue()
        {
            var clientMock = new Mock<ILdClient>();
            clientMock.Setup(c => c.StringVariationDetail("key", defaultUser, "Blue"))
                .Returns(new EvaluationDetail<string>(null, 1, EvaluationReason.FallthroughReason));
            var client = clientMock.Object;

            var result = client.EnumVariationDetail("key", defaultUser, MyEnum.Blue);
            var expected = new EvaluationDetail<MyEnum>(MyEnum.Blue, 1, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EnumVariationDetailReturnsDefaultValueForNonEnumType()
        {
            var defaultValue = "this is a string, not an enum";
            var clientMock = new Mock<ILdClient>();
            clientMock.Setup(c => c.StringVariationDetail("key", defaultUser, defaultValue))
                .Returns(new EvaluationDetail<string>("Green", 1, EvaluationReason.FallthroughReason));
            var client = clientMock.Object;

            var result = client.EnumVariationDetail("key", defaultUser, defaultValue);
            var expected = new EvaluationDetail<string>(defaultValue, 1, EvaluationReason.ErrorReason(EvaluationErrorKind.WRONG_TYPE));
            Assert.Equal(expected, result);
        }

    }
}
