using QuantumRelay.Models;

namespace QuantumRelay.Core
{
    /// <summary>
    /// Authoritative owner of routing state for Quantum Relay.
    /// </summary>
    internal sealed class QuantumManager
    {
        private static readonly QuantumManager _instance = new QuantumManager();

        private readonly QuantumRouteCache _routeCache;
        private readonly QuantumRouter _router;

        public static QuantumManager Instance { get { return _instance; } }
        public QuantumRouteCache RouteCache { get { return _routeCache; } }
        public QuantumRouter Router { get { return _router; } }

        private QuantumManager()
        {
            _routeCache = new QuantumRouteCache();
            _router = new QuantumRouter(_routeCache);
        }

        public QuantumRoute FindRoute(Vessel source, Vessel destination)
        {
            return _router.FindRoute(
                source,
                destination,
                QuantumRelayRuntimeState.Links,
                GetUniversalTime());
        }

        public void NotifyTopologyChanged()
        {
            _router.InvalidateAll();
        }

        private static double GetUniversalTime()
        {
            try { return Planetarium.GetUniversalTime(); }
            catch { return 0.0; }
        }
    }
}
