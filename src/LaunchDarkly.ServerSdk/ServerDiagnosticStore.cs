using System;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client {

    internal class ServerDiagnosticStore : IDiagnosticStore {

        public DiagnosticId DiagnosticId {
            get {
                return new DiagnosticId("fooobar");
            }
        }

        public bool SendInitEvent {
            get {
                return true;
            }
        }

        public DiagnosticEvent LastStats {
            get {
                return null;
            }
        }

        public DateTime DataSince {
            get {
                return new DateTime();
            }
        }

    }

}
