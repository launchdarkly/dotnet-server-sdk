namespace LaunchDarkly.Client.Operators
{

    internal class StartsWith : IOperatorExecutor
    {

        bool IOperatorExecutor.Execute(object userValue, object clauseValue)
        {
            string sUserValue = userValue as string;
            string sClauseValue = clauseValue as string;
            if(sUserValue != null && sClauseValue != null)
            {
                return sUserValue.StartsWith(sClauseValue);
            }
            return false;
        }

    }

}