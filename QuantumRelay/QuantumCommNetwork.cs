using CommNet;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Stock CommNetwork with one narrowly scoped exception: the two active
    /// gateway nodes are evaluated as a near-zero-distance, unobstructed relay
    /// pair. All ordinary links and all pathfinding remain stock.
    /// </summary>
    internal sealed class QuantumCommNetwork : CommNetwork
    {
        private bool _lastQuantumPairConnected;

        protected override bool SetNodeConnection(CommNode a, CommNode b)
        {
            if (!QuantumGatewayManager.IsGatewayPair(a, b))
                return base.SetNodeConnection(a, b);

            // Preserve stock link creation and stock signal-strength fields.
            // At a negligible effective distance, TryConnect calculates a real
            // CommLink that the normal shortest-path logic can traverse.
            bool connected = TryConnect(a, b, 1E-07, true, true, true);
            if (connected != _lastQuantumPairConnected)
            {
                _lastQuantumPairConnected = connected;
                Debug.Log("[QuantumRelay] Quantum graph edge " + (connected ? "CONNECTED" : "FAILED") +
                          " during stock CommNet rebuild.");
            }
            return connected;
        }
    }
}
