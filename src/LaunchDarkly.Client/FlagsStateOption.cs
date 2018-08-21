using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Optional parameters that can be passed to <see cref="ILdClient.AllFlagsState(User, FlagsStateOption[])"/>.
    /// </summary>
    public class FlagsStateOption
    {
        private readonly string _description;

        private FlagsStateOption(string description)
        {
            _description = description;
        }

        /// <summary>
        /// Returns the string representation of this option.
        /// </summary>
        /// <returns>the string representation</returns>
        public override string ToString()
        {
            return _description;
        }

        /// <summary>
        /// Specifies that only flags m arked for use with the client-side SDK should be included in the
        /// state object. By default, all flags are included.
        /// </summary>
        public static readonly FlagsStateOption ClientSideOnly = new FlagsStateOption("ClientSideOnly");

        internal static bool HasOption(FlagsStateOption[]  options, FlagsStateOption option)
        {
            foreach (var o in options)
            {
                if (o == option)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
