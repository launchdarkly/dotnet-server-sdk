using LaunchDarkly.Client.Operators;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;

namespace LaunchDarkly.Client
{

    internal static class Operator
    {

        private static readonly ILogger Logger = LdLogger.CreateLogger("Operator");

        internal static bool Apply(string op, object uValue, JValue cValue, Configuration configuration) {
            try
            {
                if(uValue == null || cValue == null)
                {
                    return false;
                }

                object convertedClauseValue = cValue.Value;
                Type userValueType = uValue.GetType();
                if(userValueType != convertedClauseValue.GetType())
                {
                    IValueConverter converter;
                    if(configuration.ValueConverters.TryGetValue(userValueType, out converter))
                    {
                        convertedClauseValue = converter.Convert(cValue.Value, userValueType);
                    }
                }

                IOperatorExecutor executor = OperatorExecutorFactory.CreateExecutor(op);
                if(executor != null)
                {
                    return executor.Execute(uValue, convertedClauseValue);
                }

                return false;

            } catch ( Exception e )
            {
                Logger.LogDebug(
                    $"Got a possibly expected exception when applying operator: {op} to user Value: {uValue} and feature flag value: {cValue}. Exception message: {e.Message}"
                );
            }
            return false;
        }

    }

}