using System.Collections.Generic;

namespace QuantumRelay.Core
{
    /// <summary>
    /// Represents a single quantum bridge connecting two gateways.
    /// </summary>
    public class QuantumBridge
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        /// <summary>Stable identity for this independent wormhole network.</summary>
        public string NetworkId { get; set; }

        public string GatewayA { get; set; }

        public string GatewayB { get; set; }

        public bool Enabled { get; set; }

        public float Health { get; set; }

        public float SignalQuality { get; set; }

        public double ActivationRange { get; set; }

        public List<string> RelayIds { get; private set; }

        public QuantumBridge()
        {
            NetworkId = string.Empty;
            Enabled = true;
            Health = 100f;
            SignalQuality = 1.0f;
            ActivationRange = 250000.0;
            RelayIds = new List<string>();
        }
    }
}