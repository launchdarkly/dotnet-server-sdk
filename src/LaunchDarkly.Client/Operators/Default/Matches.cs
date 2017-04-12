using System.Text.RegularExpressions;

namespace LaunchDarkly.Client.Operators
{

    internal class Matches : IOperatorExecutor
    {

        bool IOperatorExecutor.Execute(object userValue, object clauseValue)
        {
            string sUserValue = userValue as string;
            string sClauseValue = clauseValue as string;

            if(sUserValue != null && sClauseValue != null)
            {
                var regex = new Regex(sClauseValue);
                return regex.IsMatch(sUserValue);

            }
            return false;
        }

    }

}