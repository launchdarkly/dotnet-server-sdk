using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server
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
        /// Specifies that only flags marked for use with the client-side SDK should be included in the
        /// state object. By default, all flags are included.
        /// </summary>
        public static readonly FlagsStateOption ClientSideOnly = new FlagsStateOption("ClientSideOnly");

        /// <summary>
        /// Specifies that evaluation reasons should be included in the state object (as returned by
        /// <see cref="ILdClient.BoolVariationDetail(string, User, bool)"/>, etc.). By default, they
        /// are not included.
        /// </summary>
        public static readonly FlagsStateOption WithReasons = new FlagsStateOption("WithReasons");

        /// <summary>
        /// Specifies that any flag metadata that is normally only used for event generation - such as flag versions and
        /// evaluation reasons - should be omitted for any flag that does not have event tracking or debugging turned on.
        /// This reduces the size of the JSON data if you are passing the flag state to the front end.
        /// </summary>
        public static readonly FlagsStateOption DetailsOnlyForTrackedFlags = new FlagsStateOption("DetailsOnlyForTrackedFlags");

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
