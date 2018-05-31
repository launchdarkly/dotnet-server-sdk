using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.Common.Tests
{
    internal class SimpleClientEnvironment : ClientEnvironment
    {
        internal static readonly SimpleClientEnvironment Instance =
            new SimpleClientEnvironment();

        public override string UserAgentType { get { return "CommonClient"; } }
    }
}
