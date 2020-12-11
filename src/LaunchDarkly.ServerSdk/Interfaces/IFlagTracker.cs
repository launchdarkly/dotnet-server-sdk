using System;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// An interface for tracking changes in feature flag configurations.
    /// </summary>
    /// <remarks>
    /// An implementation of this interface is returned by <see cref="ILdClient.FlagTracker"/>.
    /// Application code never needs to implement this interface.
    /// </remarks>
    public interface IFlagTracker
    {
        /// <summary>
        /// An event for receiving notifications of feature flag changes in general.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised whenever the SDK receives any change to any feature flag's
        /// configuration, or to a user segment that is referenced by a feature flag. If the
        /// updated flag is used as a prerequisite for other flags, the SDK assumes that those
        /// flags may now behave differently and sends flag change events for them as well.
        /// </para>
        /// <para>
        /// Note that this does not necessarily mean the flag's value has changed for any
        /// particular user, only that some part of the flag configuration was changed so that
        /// it <i>may</i> return a different value than it previously returned for some user.
        /// If you want to track flag value changes, use
        /// <see cref="FlagValueChangeHandler(string, User, EventHandler{FlagValueChangeEvent})"/>.
        /// </para>
        /// <para>
        /// Change events only work if the SDK is actually connecting to LaunchDarkly (or
        /// using the file data source). If the SDK is only reading flags from a database
        /// (<see cref="Components.ExternalUpdatesOnly"/>) then it cannot know when there is a
        /// change, because flags are read on an as-needed basis.
        /// </para>
        /// <para>
        /// Notifications will be dispatched on a background task. It is the listener's
        /// responsibility to return as soon as possible so as not to block subsequent
        /// notifications.
        /// </para>
        /// </remarks>
        /// <example>
        ///     client.FlagTracker.FlagChanged += (sender, eventArgs) =>
        ///         {
        ///             System.Console.WriteLine("a flag has changed: " + eventArgs.Key);
        ///         };
        /// </example>
        event EventHandler<FlagChangeEvent> FlagChanged;

        /// <summary>
        /// Creates a handler for receiving notifications when a specific feature flag's value
        /// has changed for a specific set of user properties.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When you call this method, it first immediately evaluates the feature flag. It then
        /// returns a new event handler which you can add to the <see cref="FlagChanged"/> event.
        /// Whenever the specified feature flag changes, it re-evaluates the flag for the same
        /// user, and calls your <paramref name="handler"/> if and only if the resulting value has
        /// changed. In other words, this method filters the more general <see cref="FlagChangeEvent"/>
        /// events to produce more specific <see cref="FlagValueChangeEvent"/> events.
        /// </para>
        /// <para>
        /// All feature flag evaluations require an instance of <see cref="User"/>. If the
        /// feature flag you are tracking does not have any user targeting rules, you must still
        /// pass a dummy user such as <c>User.WithKey("for-global-flags")</c>. If you do not
        /// want the user to appear on your dashboard, use the <c>Anonymous</c> property:
        /// <c>User.Builder("for-global-flags").Anonymous(true).Build()</c>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        ///     var flagKey = "my-important-flag";
        ///     var userForFlagEvaluation = User.WithKey("user-for-evaluation");
        ///     var listenForNewValue = client.FlagTracker.FlagValueChangeHandler(
        ///         flagKey,
        ///         userForFlagEvaluation,
        ///         (sender, changeArgs) =>
        ///             {
        ///                 System.Console.WriteLine("flag '" + changeArgs.Key
        ///                     + "' changed for " + userForFlagEvaluation.Key
        ///                     + " from " + changeArgs.OldValue
        ///                     + " to " + changeArgs.NewValue);
        ///             });
        ///     client.FlagTracker.FlagChanged += listenForNewValue;
        /// </code>
        /// </example>
        /// <param name="flagKey">the flag key to be evaluated</param>
        /// <param name="user">the user properties for evaluation</param>
        /// <param name="handler">a handler that will receive a <see cref="FlagValueChangeEvent"/>
        /// </param>
        /// <returns>a handler to be added to <see cref="FlagChanged"/></returns>
        EventHandler<FlagChangeEvent> FlagValueChangeHandler(string flagKey, User user,
            EventHandler<FlagValueChangeEvent> handler);
    }

    /// <summary>
    /// A parameter class used with <see cref="IFlagTracker.FlagChanged"/>.
    /// </summary>
    /// <remarks>
    /// This is not an analytics event to be sent to LaunchDarkly; it is a notification to the
    /// application.
    /// </remarks>
    public struct FlagChangeEvent
    {
        /// <summary>
        /// The key of the feature flag whose configuration has changed.
        /// </summary>
        /// <remarks>
        /// The specified flag may have been modified directly, or this may be an indirect
        /// change due to a change in some other flag that is a prerequisite for this flag, or
        /// a user segment that is referenced in the flag's rules.
        /// </remarks>
        public string Key { get; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="key">the key of the feature flag whose configuration has changed</param>
        public FlagChangeEvent(string key)
        {
            Key = key;
        }
    }

    /// <summary>
    /// A parameter class used with <see cref="IFlagTracker.FlagValueChangeHandler"/>.
    /// </summary>
    /// <remarks>
    /// This is not an analytics event to be sent to LaunchDarkly; it is a notification to the
    /// application.
    /// </remarks>
    public struct FlagValueChangeEvent
    {
        /// <summary>
        /// The key of the feature flag whose configuration has changed.
        /// </summary>
        /// <remarks>
        /// The specified flag may have been modified directly, or this may be an indirect
        /// change due to a change in some other flag that is a prerequisite for this flag, or
        /// a user segment that is referenced in the flag's rules.
        /// </remarks>
        public string Key { get; }

        /// <summary>
        /// The last known value of the flag for the specified user prior to the update.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Since flag values can be of any JSON data type, this is represented as
        /// <see cref="LdValue"/>. That class has properties for converting to other .NET types,
        /// such as <see cref="LdValue.AsBool"/>.
        /// </para>
        /// <para>
        /// If the flag was deleted or could not be evaluated, this will be <see cref="LdValue.Null"/>.
        /// there is no application default value parameter as there is for the <c>Variation</c>
        /// methods; it is up to your code to substitute whatever fallback value is appropriate.
        /// </para>
        /// </remarks>
        public LdValue OldValue { get; }

        /// <summary>
        /// The new value of the flag for the specified user.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Since flag values can be of any JSON data type, this is represented as
        /// <see cref="LdValue"/>. That class has properties for converting to other .NET types,
        /// such as <see cref="LdValue.AsBool"/>.
        /// </para>
        /// <para>
        /// If the flag was deleted or could not be evaluated, this will be <see cref="LdValue.Null"/>.
        /// there is no application default value parameter as there is for the <c>Variation</c>
        /// methods; it is up to your code to substitute whatever fallback value is appropriate.
        /// </para>
        /// </remarks>
        public LdValue NewValue { get; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="key">the key of the feature flag whose configuration has changed</param>
        /// <param name="oldValue">he last known value of the flag for the specified user prior to
        /// the update</param>
        /// <param name="newValue">he new value of the flag for the specified user</param>
        public FlagValueChangeEvent(string key, LdValue oldValue, LdValue newValue)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
