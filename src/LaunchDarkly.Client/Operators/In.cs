using System;

namespace LaunchDarkly.Client.Operators
{

    internal sealed class In : IOperatorExecutor
    {

        bool IOperatorExecutor.Execute(object userValue, object clauseValue)
        {
            if(Util.IsNumeric(userValue))
            {
                userValue = Convert.ToDouble( userValue );
            }
            if(Util.IsNumeric(clauseValue))
            {
                clauseValue = Convert.ToDouble( clauseValue );
            }
            if(userValue.GetType() == clauseValue.GetType())
            {
                return userValue.Equals(clauseValue);
            }
            return false;
        }

    }

}