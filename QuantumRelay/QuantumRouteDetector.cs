using System;
using CommNet;

namespace QuantumRelay
{
    internal enum QuantumRouteStatus
    {
        NoCraft, GraphUnavailable, NoConnection, NoSignal, NoPath,
        InvalidPath, StalePath, OrdinaryRoute, QuantumRoute
    }
    /// <summary>Read-only inspection of the stock selected control route.</summary>
    internal static class QuantumRouteDetector
    {
        public static bool UsesQuantumRoute(Vessel vessel, QuantumCommNetwork network)
        {
            return Inspect(vessel, network) == QuantumRouteStatus.QuantumRoute;
        }

        public static bool UsesPair(Vessel vessel, QuantumCommNetwork network, CommNode a, CommNode b)
        {
            if (a == null || b == null || !QuantumGatewayManager.IsGatewayPair(a, b) ||
                !UsesQuantumRoute(vessel, network)) return false;
            foreach (CommLink hop in vessel.connection.ControlPath)
                if ((ReferenceEquals(hop.a, a) && ReferenceEquals(hop.b, b)) ||
                    (ReferenceEquals(hop.a, b) && ReferenceEquals(hop.b, a))) return true;
            return false;
        }

        public static QuantumRouteStatus Inspect(Vessel vessel, QuantumCommNetwork network)
        {
            if (vessel == null) return QuantumRouteStatus.NoCraft;
            if (network == null) return QuantumRouteStatus.GraphUnavailable;
            if (vessel.connection == null) return QuantumRouteStatus.NoConnection;
            if (!vessel.connection.IsConnected) return QuantumRouteStatus.NoSignal;

            CommPath path = vessel.connection.ControlPath;
            if (path == null || path.Count == 0)
                return QuantumRouteStatus.NoPath;

            bool quantum = false;
            for (int i = 0; i < path.Count; i++)
            {
                CommLink link = path[i];
                if (link == null || link.a == null || link.b == null)
                    return QuantumRouteStatus.InvalidPath;

                // KSP creates separate CommLinks for the selected ControlPath.
                // Validate the exact live node pair, not the copied link object.
                // Names/IDs alone could match nodes from an obsolete graph.
                bool current = false;
                foreach (CommLink active in network.Links)
                {
                    if (active != null &&
                        ((ReferenceEquals(active.a, link.a) && ReferenceEquals(active.b, link.b)) ||
                         (ReferenceEquals(active.a, link.b) && ReferenceEquals(active.b, link.a))))
                    {
                        current = true;
                        break;
                    }
                }
                if (!current) return QuantumRouteStatus.StalePath;
                if (QuantumGatewayManager.IsGatewayPair(link.a, link.b))
                    quantum = true;
            }
            return quantum ? QuantumRouteStatus.QuantumRoute : QuantumRouteStatus.OrdinaryRoute;
        }
    }
}
