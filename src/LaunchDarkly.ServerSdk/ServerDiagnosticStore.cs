using System;
using System.Collections.Generic;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client {

    internal class ServerDiagnosticStore : IDiagnosticStore {
        readonly DiagnosticId DiagnosticId;
        DateTime DataSince;
        long DroppedEvents;
        long DeduplicatedUsers;

        DiagnosticId IDiagnosticStore.DiagnosticId => DiagnosticId;
        bool IDiagnosticStore.SendInitEvent { get; } = true;
        Dictionary<string, Object> IDiagnosticStore.LastStats { get; } = null;
        DateTime IDiagnosticStore.DataSince => DataSince;

        internal ServerDiagnosticStore(string sdkKey) {
            DataSince = DateTime.UtcNow;
            DiagnosticId = new DiagnosticId(sdkKey, Guid.NewGuid());
        }

        public void IncrementDeduplicatedUsers() {
            this.DeduplicatedUsers++;
        }

        public void IncrementDroppedEvents() {
            this.DroppedEvents++;
        }

        public Dictionary<string, Object> GetStatsAndReset(long eventsInQueue)
        {
            DateTime CurrentTime = DateTime.UtcNow;
            Dictionary<string, Object> stats = new Dictionary<string, Object>();
            stats["droppedEvents"] = DroppedEvents;
            stats["deduplicatedUsers"] = DeduplicatedUsers;
            stats["eventsInQueue"] = eventsInQueue;
            stats["dataSinceDate"] = Util.GetUnixTimestampMillis(DataSince);
            stats["currentTime"] = Util.GetUnixTimestampMillis(CurrentTime);
            stats["id"] = DiagnosticId;
            stats["kind"] = "diagnostic";

            DataSince = CurrentTime;
            DroppedEvents = 0;
            DeduplicatedUsers = 0;

            return stats;
        }
    }
}
