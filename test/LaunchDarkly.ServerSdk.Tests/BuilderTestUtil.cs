using System;
using Xunit;

namespace LaunchDarkly.Sdk.Server
{
    public static class BuilderTestUtil
    {
        // Use this when we want to test the effect of builder changes on the object that
        // is eventually built.
        public static BuilderTestUtil<T, U> For<T, U>(
            Func<T> constructor, Func<T, U> buildMethod) =>
            new BuilderTestUtil<T, U>(constructor, buildMethod, null);

        // Use this when we want to test the builder's internal state directly, without
        // calling Build - i.e. if the object is difficult to inspect after it's built.
        public static BuilderInternalTestUtil<T> For<T>(Func<T> constructor) =>
            new BuilderInternalTestUtil<T>(constructor);
    }

    public class BuilderTestUtil<TBuilder, TBuilt>
    {
        private readonly Func<TBuilder> _constructor;
        internal readonly Func<TBuilder, TBuilt> _buildMethod;
        internal readonly Func<TBuilt, TBuilder> _copyConstructor;

        public BuilderTestUtil(Func<TBuilder> constructor,
            Func<TBuilder, TBuilt> buildMethod,
            Func<TBuilt, TBuilder> copyConstructor
            )
        {
            _constructor = constructor;
            _buildMethod = buildMethod;
            _copyConstructor = copyConstructor;
        }

        public BuilderPropertyTestUtil<TBuilder, TBuilt, TValue> Property<TValue>(
            Func<TBuilt, TValue> getter,
            Action<TBuilder, TValue> builderSetter
            ) =>
            new BuilderPropertyTestUtil<TBuilder, TBuilt, TValue>(
                this, getter, builderSetter);

        public TBuilder New() => _constructor();

        public BuilderTestUtil<TBuilder, TBuilt> WithCopyConstructor(
            Func<TBuilt, TBuilder> copyConstructor
            ) =>
            new BuilderTestUtil<TBuilder, TBuilt>(_constructor, _buildMethod, copyConstructor);
    }

    public class BuilderPropertyTestUtil<TBuilder, TBuilt, TValue>
    {
        private readonly BuilderTestUtil<TBuilder, TBuilt> _owner;
        private readonly Func<TBuilt, TValue> _getter;
        private readonly Action<TBuilder, TValue> _builderSetter;

        public BuilderPropertyTestUtil(BuilderTestUtil<TBuilder, TBuilt> owner,
            Func<TBuilt, TValue> getter,
            Action<TBuilder, TValue> builderSetter)
        {
            _owner = owner;
            _getter = getter;
            _builderSetter = builderSetter;
        }

        public void AssertDefault(TValue defaultValue)
        {
            var b = _owner.New();
            AssertValue(b, defaultValue);
        }

        public void AssertCanSet(TValue newValue)
        {
            AssertSetIsChangedTo(newValue, newValue);
        }

        public void AssertSetIsChangedTo(TValue attemptedValue, TValue resultingValue)
        {
            var b = _owner.New();
            _builderSetter(b, attemptedValue);
            AssertValue(b, resultingValue);
        }

        private void AssertValue(TBuilder b, TValue v)
        {
            var o = _owner._buildMethod(b);
            Assert.Equal(v, _getter(o));
            if (_owner._copyConstructor != null)
            {
                var b1 = _owner._copyConstructor(o);
                var o1 = _owner._buildMethod(b);
                Assert.Equal(v, _getter(o));
            }
        }
    }

    public class BuilderInternalTestUtil<TBuilder>
    {
        private readonly Func<TBuilder> _constructor;

        public BuilderInternalTestUtil(Func<TBuilder> constructor)
        {
            _constructor = constructor;
        }

        public BuilderInternalPropertyTestUtil<TBuilder, TValue> Property<TValue>(
            Func<TBuilder, TValue> builderGetter,
            Action<TBuilder, TValue> builderSetter
            ) =>
            new BuilderInternalPropertyTestUtil<TBuilder, TValue>(this,
                builderGetter, builderSetter);

        public TBuilder New() => _constructor();
    }

    public class BuilderInternalPropertyTestUtil<TBuilder, TValue>
    {
        private readonly BuilderInternalTestUtil<TBuilder> _owner;
        private readonly Func<TBuilder, TValue> _builderGetter;
        private readonly Action<TBuilder, TValue> _builderSetter;

        public BuilderInternalPropertyTestUtil(BuilderInternalTestUtil<TBuilder> owner,
            Func<TBuilder, TValue> builderGetter,
            Action<TBuilder, TValue> builderSetter)
        {
            _owner = owner;
            _builderGetter = builderGetter;
            _builderSetter = builderSetter;
        }

        public void AssertDefault(TValue defaultValue)
        {
            Assert.Equal(defaultValue, _builderGetter(_owner.New()));
        }

        public void AssertCanSet(TValue newValue)
        {
            AssertSetIsChangedTo(newValue, newValue);
        }

        public void AssertSetIsChangedTo(TValue attemptedValue, TValue resultingValue)
        {
            var b = _owner.New();
            _builderSetter(b, attemptedValue);
            Assert.Equal(resultingValue, _builderGetter(b));
        }
    }
}
