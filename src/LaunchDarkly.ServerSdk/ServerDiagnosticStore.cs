using System;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client {

    internal class ServerDiagnosticStore : IDiagnosticStore {
        readonly DiagnosticId DiagnosticId;
        DateTime DataSince;

        DiagnosticId IDiagnosticStore.DiagnosticId => DiagnosticId;
        bool IDiagnosticStore.SendInitEvent { get; } = true;
        DiagnosticEvent IDiagnosticStore.LastStats { get; } = null;
        DateTime IDiagnosticStore.DataSince => DataSince;

        internal ServerDiagnosticStore(string sdkKey) {
            DataSince = DateTime.UtcNow;
            DiagnosticId = new DiagnosticId(sdkKey, Guid.NewGuid());
        }

        public DiagnosticEvent.Statistics CreateEventAndReset(long droppedEvents, long deduplicatedUsers, long eventsInQueue)
        {
            DateTime currentTime = DateTime.UtcNow;
            DiagnosticEvent.Statistics res = new DiagnosticEvent.Statistics(Util.GetUnixTimestampMillis(currentTime), DiagnosticId,
                                                                            Util.GetUnixTimestampMillis(DataSince), droppedEvents,
                                                                            deduplicatedUsers, eventsInQueue);
            DataSince = currentTime;
            return res;
        }
    }
}
