using LaunchDarkly.Sdk.Server.Internal.Model;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    internal partial class Evaluator
    {
        private int? MatchTargets(ref EvalState state, FeatureFlag flag)
        {
            if (flag.ContextTargets.IsEmpty)
            {
                // old-style data has only targets for users
                if (!state.Context.TryGetContextByKind(ContextKind.Default, out var matchContext))
                {
                    return null;
                }
                foreach (var t in flag.Targets)
                {
                    if (TargetHasKey(t, matchContext.Key))
                    {
                        return t.Variation;
                    }
                }
                return null;
            }

            // new-style data has ContextTargets, which may include placeholders for user targets that are in Targets
            foreach (var t in flag.ContextTargets)
            {
                var contextKind = t.ContextKind ?? ContextKind.Default;
                if (contextKind.IsDefault)
                {
                    if (!state.Context.TryGetContextByKind(contextKind, out var matchContext))
                    {
                        continue;
                    }
                    foreach (var ut in flag.Targets)
                    {
                        if (ut.Variation == t.Variation)
                        {
                            if (TargetHasKey(ut, matchContext.Key))
                            {
                                return ut.Variation;
                            }
                            break;
                        }
                    }
                }
                else
                {
                    if (state.Context.TryGetContextByKind(contextKind, out var matchContext) &&
                        TargetHasKey(t, matchContext.Key))
                    {
                        return t.Variation;
                    }
                }
            }
            return null;
        }

        private static bool TargetHasKey(in Target t, string key) =>
            t.Preprocessed.ValuesSet.Contains(key);
    }
}
