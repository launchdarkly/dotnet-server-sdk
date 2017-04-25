using LaunchDarkly.Client.Operators;
using Microsoft.Extensions.Logging;
using System;
using LaunchDarkly.Client.CustomAttributes;

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

                IOperatorExecutor executor;
                if(!OperatorExecutorFactory.TryCreateExecutor(op, out executor))
                {
                    return false;
                }

                return executor.Execute(userValue, clauseValue);

            } catch ( Exception e )
            {
                Logger.LogDebug(
                    "Got a possibly expected exception when applying operator: " +
                    $"\"{op}\" to user Value: {userValue} of type \"{userValue?.GetType().AssemblyQualifiedName}\" and " +
                    $"feature flag value: {clauseValue} of type \"{clauseValue?.GetType().AssemblyQualifiedName}\". " +
                    $"Exception: {e}"
                );
            }
            return false;
        }

    }

}