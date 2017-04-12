using LaunchDarkly.Client.Operators;
using Microsoft.Extensions.Logging;
using System;

namespace LaunchDarkly.Client {

    internal static class Operator
    {

        private static readonly ILogger Logger = LdLogger.CreateLogger("Operator");

        internal static bool Apply(string op, object userValue, object clauseValue, Configuration configuration) {
            try
            {
                if(userValue == null || clauseValue == null)
                {
                    return false;
                }

                Type userValueType = userValue.GetType();
                if(userValueType != clauseValue.GetType())
                {
                    ITypeConverter converter;
                    if(configuration.ValueConverters.TryGetValue(userValueType, out converter))
                    {
                        clauseValue = converter.Convert(clauseValue, userValueType);
                    }
                }

                IOperatorExecutor executor = OperatorExecutorFactory.CreateExecutor(op);
                if(executor != null)
                {
                    return executor.Execute(userValue, clauseValue);
                }

                return false;

            } catch ( Exception e )
            {
                Logger.LogDebug(
                    $"Got a possibly expected exception when applying operator: {op} to user Value: {userValue} and feature flag value: {clauseValue}. Exception message: {e.Message}"
                );
            }
            return false;
        }

    }

}