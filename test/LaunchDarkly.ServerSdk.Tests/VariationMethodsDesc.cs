using System;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server
{
    public struct VariationMethodsDesc<T>
    {
        public Func<ILdClient, string, Context, T, T> VariationMethod;
        public Func<ILdClient, string, User, T, T> VariationForUserMethod;
        public Func<ILdClient, string, Context, T, EvaluationDetail<T>> VariationDetailMethod;
        public Func<ILdClient, string, User, T, EvaluationDetail<T>> VariationDetailForUserMethod;
        public T ExpectedValue;
        public LdValue ExpectedLdValue;
        public T DefaultValue;
        public LdValue DefaultLdValue;
        public LdValue WrongTypeLdValue;
    }

    public static class VariationMethodsDesc
    {
        public static VariationMethodsDesc<bool> Bool = new VariationMethodsDesc<bool>
        {
            VariationMethod = (c, f, ctx, d) => c.BoolVariation(f, ctx, d),
            VariationForUserMethod = (c, f, u, d) => c.BoolVariation(f, u, d),
            VariationDetailMethod = (c, f, ctx, d) => c.BoolVariationDetail(f, ctx, d),
            VariationDetailForUserMethod = (c, f, u, d) => c.BoolVariationDetail(f, u, d),
            ExpectedValue = true,
            ExpectedLdValue = LdValue.Of(true),
            DefaultValue = false,
            DefaultLdValue = LdValue.Of(false),
            WrongTypeLdValue = LdValue.Of("wrongtype")
        };

        public static VariationMethodsDesc<int> Int = new VariationMethodsDesc<int>
        {
            VariationMethod = (c, f, ctx, d) => c.IntVariation(f, ctx, d),
            VariationForUserMethod = (c, f, u, d) => c.IntVariation(f, u, d),
            VariationDetailMethod = (c, f, ctx, d) => c.IntVariationDetail(f, ctx, d),
            VariationDetailForUserMethod = (c, f, u, d) => c.IntVariationDetail(f, u, d),
            ExpectedValue = 100,
            ExpectedLdValue = LdValue.Of(100),
            DefaultValue = 99,
            DefaultLdValue = LdValue.Of(99),
            WrongTypeLdValue = LdValue.Of("wrongtype")
        };

        public static VariationMethodsDesc<float> Float = new VariationMethodsDesc<float>
        {
            VariationMethod = (c, f, ctx, d) => c.FloatVariation(f, ctx, d),
            VariationForUserMethod = (c, f, u, d) => c.FloatVariation(f, u, d),
            VariationDetailMethod = (c, f, ctx, d) => c.FloatVariationDetail(f, ctx, d),
            VariationDetailForUserMethod = (c, f, u, d) => c.FloatVariationDetail(f, u, d),
            ExpectedValue = 100.5f,
            ExpectedLdValue = LdValue.Of(100.5f),
            DefaultValue = 99.5f,
            DefaultLdValue = LdValue.Of(99.5f),
            WrongTypeLdValue = LdValue.Of("wrongtype")
        };

        public static VariationMethodsDesc<double> Double = new VariationMethodsDesc<double>
        {
            VariationMethod = (c, f, ctx, d) => c.DoubleVariation(f, ctx, d),
            VariationForUserMethod = (c, f, u, d) => c.DoubleVariation(f, u, d),
            VariationDetailMethod = (c, f, ctx, d) => c.DoubleVariationDetail(f, ctx, d),
            VariationDetailForUserMethod = (c, f, u, d) => c.DoubleVariationDetail(f, u, d),
            ExpectedValue = 100.5d,
            ExpectedLdValue = LdValue.Of(100.5d),
            DefaultValue = 99.5d,
            DefaultLdValue = LdValue.Of(99.5d),
            WrongTypeLdValue = LdValue.Of("wrongtype")
        };

        public static VariationMethodsDesc<string> String = new VariationMethodsDesc<string>
        {
            VariationMethod = (c, f, ctx, d) => c.StringVariation(f, ctx, d),
            VariationForUserMethod = (c, f, u, d) => c.StringVariation(f, u, d),
            VariationDetailMethod = (c, f, ctx, d) => c.StringVariationDetail(f, ctx, d),
            VariationDetailForUserMethod = (c, f, u, d) => c.StringVariationDetail(f, u, d),
            ExpectedValue = "value",
            ExpectedLdValue = LdValue.Of("value"),
            DefaultValue = "defaultvalue",
            DefaultLdValue = LdValue.Of("defaultvalue"),
            WrongTypeLdValue = LdValue.Of(3)
        };

        public static VariationMethodsDesc<LdValue> Json = new VariationMethodsDesc<LdValue>
        {
            VariationMethod = (c, f, ctx, d) => c.JsonVariation(f, ctx, d),
            VariationForUserMethod = (c, f, u, d) => c.JsonVariation(f, u, d),
            VariationDetailMethod = (c, f, ctx, d) => c.JsonVariationDetail(f, ctx, d),
            VariationDetailForUserMethod = (c, f, u, d) => c.JsonVariationDetail(f, u, d),
            ExpectedValue = LdValue.ArrayOf(LdValue.Of(1), LdValue.Of("a")),
            ExpectedLdValue = LdValue.ArrayOf(LdValue.Of(1), LdValue.Of("a")),
            DefaultValue = LdValue.Of("defaultvalue"),
            DefaultLdValue = LdValue.Of("defaultvalue"),
            WrongTypeLdValue = LdValue.Null
        };
    }
}

