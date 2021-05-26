
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IBigSegmentStore"/>.
    /// </summary>
    public interface IBigSegmentStoreFactory
    {
        /// <summary>
        /// Called internally by the SDK to create an implementation instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <param name="context">configuration of the current client instance</param>
        /// <returns>an <see cref="IBigSegmentStore"/> instance</returns>
        IBigSegmentStore CreateBigSegmentStore(LdClientContext context);
    }
}
