using System.Collections.Generic;

namespace LaunchDarkly.Client.Operators
{

    internal class OperatorExecutorFactory
    {

        private static readonly IDictionary<string, IOperatorExecutor> Executors =
            new Dictionary<string, IOperatorExecutor>
            {
                { "in", new In() },
                { "endsWith", new EndsWith() },
                { "startsWith", new StartsWith() },
                { "matches", new Matches() },
                { "contains", new Contains() },
                { "lessThan", new LessThan() },
                { "lessThanOrEqual", new LessThanOrEqual() },
                { "greaterThan", new GreatedThan() },
                { "greaterThanOrEqual", new GreaterThanOrEqual() },
                { "before", new Before() },
                { "after", new After() }
            };

        public static bool TryCreateExecutor(string op, out IOperatorExecutor executor)
        {
            return Executors.TryGetValue(op, out executor);
        }

    }

}