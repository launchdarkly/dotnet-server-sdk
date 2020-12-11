using System;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;

namespace LaunchDarkly.Sdk.Server.Internal
{
    /// <summary>
    /// Abstraction of scheduling infrequent worker tasks.
    /// </summary>
    /// <remarks>
    /// We use this instead of just calling <c>Task.Run()</c> for two reasons. First, the default
    /// scheduling behavior of <c>Task.Run()</c> may not always be what we want. Second, this provides
    /// better error logging.
    /// </remarks>
    internal sealed class TaskExecutor
    {
        private readonly Logger _log;

        internal TaskExecutor(Logger log)
        {
            _log = log;
        }

        /// <summary>
        /// Schedules delivery of an event to some number of event handlers.
        /// </summary>
        /// <remarks>
        /// In the current implementation, each handler call is a separate background task.
        /// </remarks>
        /// <typeparam name="T">the event type</typeparam>
        /// <param name="sender">passed as the <c>sender</c> parameter to the handlers</param>
        /// <param name="eventArgs">the event object</param>
        /// <param name="handlers">a handler list</param>
        public void ScheduleEvent<T>(object sender, T eventArgs, EventHandler<T> handlers)
        {
            if (handlers is null)
            {
                return;
            }
            _log.Debug("scheduling task to send {0} to {1}", eventArgs, handlers);
            Task.Run(() =>
            {
                _log.Debug("sending {0} to {1}", eventArgs, handlers);
                try
                {
                    handlers.DynamicInvoke(sender, eventArgs);
                }
                catch (Exception e)
                {
                    LogHelpers.LogException(_log, "Unexpected exception from event handler", e);
                }
            });
        }
    }
}
