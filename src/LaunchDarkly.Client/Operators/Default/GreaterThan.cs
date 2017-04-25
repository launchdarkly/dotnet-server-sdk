using System;

namespace LaunchDarkly.Client.Operators
{

    internal class GreaterThan : IOperatorExecutor
    {

        bool IOperatorExecutor.Execute(object userValue, object clauseValue)
        {
            if(Util.IsNumeric(userValue))
            {
                userValue = Convert.ToDouble(userValue);
            }
            if(Util.IsNumeric(clauseValue))
            {
                clauseValue = Convert.ToDouble(clauseValue);
            }

            IComparable cUserValue = userValue as IComparable;
            IComparable cClauseValue = clauseValue as IComparable;
            if(cUserValue != null && cClauseValue != null)
            {
                return cUserValue.CompareTo(cClauseValue) > 0;
            }
            return false;
        }

    }

}