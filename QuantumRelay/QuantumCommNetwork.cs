using CommNet;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Stock CommNetwork with one narrowly scoped exception: the two active
    /// gateway nodes are evaluated as an unobstructed relay pair. The resulting
    /// link strengths are capped to the configured quantum-link quality.
    /// </summary>
    internal sealed class QuantumCommNetwork : CommNetwork
    {
        private bool _lastQuantumPairConnected;

        protected override bool SetNodeConnection(CommNode a, CommNode b)
        {
            if (!QuantumGatewayManager.IsGatewayPair(a, b))
                return base.SetNodeConnection(a, b);

            bool connected = TryConnect(a, b, 1E-07, true, true, true);
            if (connected)
            {
                // Connect returns the same graph edge created by TryConnect.
                // Setting all three stock route strengths makes the configured
                // percentage participate naturally in CommNet path quality and
                // science-transmission calculations.
                CommLink link = Connect(a, b, 1E-07);
                double quality = QuantumGatewayManager.GetPairSignalQuality(a, b);
                if (quality <= 0.0) quality = QuantumRelaySettings.SignalQualityMultiplier;
                link.strengthRR = quality;
                link.strengthAR = quality;
                link.strengthBR = quality;
                link.aCanRelay = true;
                link.bCanRelay = true;
                link.bothRelay = true;
            }

            if (connected != _lastQuantumPairConnected)
            {
                _lastQuantumPairConnected = connected;
                Debug.Log("[QuantumRelay] Quantum graph edge " + (connected ? "CONNECTED" : "FAILED") +
                          " | quality=" + (QuantumGatewayManager.GetPairSignalQuality(a, b) * 100.0).ToString("N0") + "%");
            }
            return connected;
        }
    }
}
