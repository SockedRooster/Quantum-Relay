using System;

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

        public static GatewayCandidate GatewayA => _gatewayA;
        public static GatewayCandidate GatewayB => _gatewayB;
        public static bool Online => _online;
        public static string Reason => _reason;
        public static string Ticker => _ticker;
        public static double UpdatedUt => _updatedUt;

        public static void Publish(GatewayCandidate gatewayA, GatewayCandidate gatewayB, bool online, string reason)
        {
            _gatewayA = gatewayA;
            _gatewayB = gatewayB;
            _online = online;
            _reason = string.IsNullOrEmpty(reason) ? "unknown" : reason;
            try { _updatedUt = Planetarium.GetUniversalTime(); }
            catch { _updatedUt = 0.0; }
            QuantumRelayRegistry.Publish(gatewayA, gatewayB, online, _reason, true);
        }

        public static void SetTicker(string text)
        {
            if (!string.IsNullOrEmpty(text)) _ticker = text;
        }

        public static void Clear()
        {
            _gatewayA = null;
            _gatewayB = null;
            _online = false;
            _reason = "not running";
            _ticker = "Quantum Relay stopped.";
            _updatedUt = 0.0;
        }
    }
}
