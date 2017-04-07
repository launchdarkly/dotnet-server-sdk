using System;

namespace LaunchDarkly.Client.Operators
{

    /// <summary>
    /// Can ony be applied to <see cref="DateTime"/> objects
    /// </summary>
    internal class After : IOperatorExecutor
    {

        bool IOperatorExecutor.Execute(object userValue, object clauseValue) {
            if(clauseValue == null)
            {
                return false;
            }
            if(!( userValue is DateTime ))
            {
                return false;
            }

            clauseValue = Util.ObjectToDateTime(clauseValue);
            IComparable cUserValue = userValue as IComparable;
            IComparable cClauseValue = clauseValue as IComparable;

            return cUserValue.CompareTo(cClauseValue) > 0;
        }

    }

}