namespace LaunchDarkly.Client.Operators
{

    internal interface IOperatorExecutor
    {

        bool Execute(object userValue, object clauseValue);

    }

}