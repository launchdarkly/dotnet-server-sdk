using System;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataSources;

namespace LaunchDarkly.Sdk.Server.Internal
{
    internal sealed class FlagTrackerImpl : IFlagTracker
    {
        private readonly DataSourceUpdatesImpl _dataSourceUpdates;
        private readonly Func<string, User, LdValue> _evaluateFn;

        public event EventHandler<FlagChangeEvent> FlagChanged
        {
            add
            {
                _dataSourceUpdates.FlagChanged += value;
            }
            remove
            {
                _dataSourceUpdates.FlagChanged -= value;
            }
        }

        internal FlagTrackerImpl(
            DataSourceUpdatesImpl dataSourceUpdates,
            Func<string, User, LdValue> evaluateFn
            )
        {
            _dataSourceUpdates = dataSourceUpdates;
            _evaluateFn = evaluateFn;
        }

        public EventHandler<FlagChangeEvent> FlagValueChangeHandler(
            string flagKey,
            User user,
            EventHandler<FlagValueChangeEvent> handler
            )
        {
            var monitor = new FlagValueChangeMonitor(_evaluateFn, flagKey,
                user, handler);
            return monitor.OnFlagChanged;
        }

        private sealed class FlagValueChangeMonitor
        {
            private readonly Func<string, User, LdValue> _evaluateFn;
            private readonly string _flagKey;
            private readonly User _user;
            private readonly EventHandler<FlagValueChangeEvent> _handler;
            private readonly object _valueLock = new object();

            private LdValue _value;

            internal FlagValueChangeMonitor(
                Func<string, User, LdValue> evaluateFn,
                string flagKey,
                User user,
                EventHandler<FlagValueChangeEvent> handler
                )
            {
                _evaluateFn = evaluateFn;
                _flagKey = flagKey;
                _user = user;
                _handler = handler;

                _value = evaluateFn(flagKey, user);
            }

            internal void OnFlagChanged(object sender, FlagChangeEvent eventArgs)
            {
                if (eventArgs.Key != _flagKey)
                {
                    return;
                }
                var newValue = _evaluateFn(_flagKey, _user);
                LdValue oldValue = LdValue.Null;
                bool changed = false;
                lock (_valueLock)
                {
                    if (!_value.Equals(newValue))
                    {
                        changed = true;
                        oldValue = _value;
                        _value = newValue;
                    }
                }
                if (changed)
                {
                    _handler(sender, new FlagValueChangeEvent(_flagKey, oldValue, newValue));
                }
            }
        }
    }
}
