using System;
using LaunchDarkly.Client;

namespace LaunchDarkly.Common
{
    /// <summary>
    /// Interface for a strategy for removing duplicate users from the event stream. This has
    /// been factored out of <see cref="DefaultEventProcessor"/> because the client-side and
    /// server-side clients behave differently (client-side does not send index events).
    /// </summary>
    internal interface IUserDeduplicator
    {
        /// <summary>
        /// The interval, if any, at which the event processor should call Flush.
        /// </summary>
        TimeSpan? FlushInterval { get; }

        /// <summary>
        /// Updates the internal state if necessary to reflect that we have seen the given user.
        /// Returns true if it is time to insert an index event for this user into the event output.
        /// </summary>
        /// <param name="user">a user object</param>
        /// <returns>true if an index event should be emitted</returns>
        bool ProcessUser(User user);

        /// <summary>
        /// Forgets any cached user information, so all subsequent users will be treated as new.
        /// </summary>
        void Flush();
    }
}
