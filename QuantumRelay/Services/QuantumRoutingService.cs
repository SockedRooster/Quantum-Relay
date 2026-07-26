using QuantumRelay.Core;
using QuantumRelay.Models;

namespace QuantumRelay.Services
{
    /// <summary>
    /// Internal facade used by future CommNet integration and diagnostics.
    /// </summary>
    internal static class QuantumRoutingService
    {
        public static QuantumRoute FindRoute(Vessel source, Vessel destination)
        {
            return QuantumManager.Instance.FindRoute(source, destination);
        }

        public static void InvalidateRoutes()
        {
            QuantumManager.Instance.NotifyTopologyChanged();
        }
    }
}
