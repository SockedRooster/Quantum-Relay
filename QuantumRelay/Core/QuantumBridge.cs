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

        public string GatewayA { get; set; }

        public string GatewayB { get; set; }

        public bool Enabled { get; set; }

        public float Health { get; set; }

        public float SignalQuality { get; set; }

        public double ActivationRange { get; set; }

        public List<string> RelayIds { get; private set; }

        public QuantumBridge()
        {
            Enabled = true;
            Health = 100f;
            SignalQuality = 1.0f;
            ActivationRange = 250000.0;
            RelayIds = new List<string>();
        }
    }
}