using System;

namespace QuantumRelay.Core
{
    /// <summary>Runtime state for one independently powered wormhole link.</summary>
    internal sealed class ActiveQuantumLink
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public GatewayCandidate GatewayA { get; set; }
        public GatewayCandidate GatewayB { get; set; }
        public bool Online { get; set; }
        public string Reason { get; set; }

        public string SafeDisplayName
        {
            get { return string.IsNullOrEmpty(DisplayName) ? Id : DisplayName; }
        }

        public bool HasValidGateways
        {
            get
            {
                return GatewayA != null && GatewayB != null &&
                       GatewayA.Vessel != null && GatewayB.Vessel != null &&
                       GatewayA.IsValid && GatewayB.IsValid &&
                       GatewayA.Vessel.id != GatewayB.Vessel.id;
            }
        }

        public ActiveQuantumLink()
        {
            Id = Guid.NewGuid().ToString("N");
            DisplayName = "Quantum Link";
            Reason = "initialising";
        }
    }
}
