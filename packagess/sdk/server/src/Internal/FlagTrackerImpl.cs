using System;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataSources;

namespace LaunchDarkly.Sdk.Server.Internal
{
    internal sealed class FlagTrackerImpl : IFlagTracker
    {
        private readonly DataSourceUpdatesImpl _dataSourceUpdates;
        private readonly Func<string, Context, LdValue> _evaluateFn;

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
            Func<string, Context, LdValue> evaluateFn
            )
        {
            _dataSourceUpdates = dataSourceUpdates;
            _evaluateFn = evaluateFn;
        }

        public EventHandler<FlagChangeEvent> FlagValueChangeHandler(
            string flagKey,
            Context context,
            EventHandler<FlagValueChangeEvent> handler
            )
        {
            var monitor = new FlagValueChangeMonitor(_evaluateFn, flagKey,
                context, handler);
            return monitor.OnFlagChanged;
        }

        private sealed class FlagValueChangeMonitor
        {
            private readonly Func<string, Context, LdValue> _evaluateFn;
            private readonly string _flagKey;
            private readonly Context _context;
            private readonly EventHandler<FlagValueChangeEvent> _handler;
            private readonly object _valueLock = new object();

            private LdValue _value;

            internal FlagValueChangeMonitor(
                Func<string, Context, LdValue> evaluateFn,
                string flagKey,
                Context context,
                EventHandler<FlagValueChangeEvent> handler
                )
            {
                _evaluateFn = evaluateFn;
                _flagKey = flagKey;
                _context = context;
                _handler = handler;

                _value = evaluateFn(flagKey, context);
            }

            internal void OnFlagChanged(object sender, FlagChangeEvent eventArgs)
            {
                if (eventArgs.Key != _flagKey)
                {
                    return;
                }
                var newValue = _evaluateFn(_flagKey, _context);
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
