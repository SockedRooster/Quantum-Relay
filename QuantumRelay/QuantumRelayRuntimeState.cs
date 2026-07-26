using System.Collections.Generic;
using QuantumRelay.Core;

namespace QuantumRelay
{
    /// <summary>Read-only runtime snapshot shared by the flight controller and GUI.</summary>
    internal static class QuantumRelayRuntimeState
    {
        private static GatewayCandidate _gatewayA;
        private static GatewayCandidate _gatewayB;
        private static bool _online;
        private static string _reason = "initialising";
        private static string _ticker = "Initialising Quantum Relay...";
        private static double _updatedUt;
        private static readonly List<ActiveQuantumLink> _links = new List<ActiveQuantumLink>();

        public static GatewayCandidate GatewayA { get { return _gatewayA; } }
        public static GatewayCandidate GatewayB { get { return _gatewayB; } }
        public static bool Online { get { return _online; } }
        public static string Reason { get { return _reason; } }
        public static string Ticker { get { return _ticker; } }
        public static double UpdatedUt { get { return _updatedUt; } }
        public static IList<ActiveQuantumLink> Links { get { return _links.AsReadOnly(); } }
        public static int ActiveLinkCount { get { return _links.FindAll(l => l != null && l.Online).Count; } }

        public static void PublishLinks(IEnumerable<ActiveQuantumLink> links, string reason)
        {
            _links.Clear();
            if (links != null) _links.AddRange(links);

            ActiveQuantumLink first = _links.Find(l => l != null && l.Online) ??
                                      _links.Find(l => l != null);
            _gatewayA = first != null ? first.GatewayA : null;
            _gatewayB = first != null ? first.GatewayB : null;
            _online = ActiveLinkCount > 0;
            _reason = string.IsNullOrEmpty(reason) ? (_online ? "ready" : "offline") : reason;
            try { _updatedUt = Planetarium.GetUniversalTime(); } catch { _updatedUt = 0.0; }
            QuantumRelayRegistry.Publish(_gatewayA, _gatewayB, _online, _reason, true);
        }

        public static void Publish(GatewayCandidate gatewayA, GatewayCandidate gatewayB, bool online, string reason)
        {
            ActiveQuantumLink link = new ActiveQuantumLink
            {
                Id = "legacy",
                DisplayName = "Primary Quantum Link",
                GatewayA = gatewayA,
                GatewayB = gatewayB,
                Online = online,
                Reason = reason
            };
            PublishLinks(new[] { link }, reason);
        }

        public static void SetTicker(string text) { if (!string.IsNullOrEmpty(text)) _ticker = text; }

        public static void Clear()
        {
            _links.Clear();
            _gatewayA = null;
            _gatewayB = null;
            _online = false;
            _reason = "not running";
            _ticker = "Quantum Relay stopped.";
            _updatedUt = 0.0;
        }
    }
}
