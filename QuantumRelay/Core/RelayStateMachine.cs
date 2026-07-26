namespace QuantumRelay
{
    internal sealed class RelayStateMachine
    {
        public QuantumRelayOperationalState Evaluate(
            bool relayEnabled,
            QuantumRelayDeploymentState deploymentState,
            bool requiresCommNetHardware,
            bool hasCommNetHardware,
            bool hasPower,
            bool isSynchronized)
        {
            if (!relayEnabled)
                return QuantumRelayOperationalState.Disabled;

            switch (deploymentState)
            {
                case QuantumRelayDeploymentState.Retracted:
                    return QuantumRelayOperationalState.Retracted;

                case QuantumRelayDeploymentState.Extending:
                    return QuantumRelayOperationalState.Deploying;

                case QuantumRelayDeploymentState.Retracting:
                    return QuantumRelayOperationalState.Retracting;

                case QuantumRelayDeploymentState.Missing:
                case QuantumRelayDeploymentState.Broken:
                case QuantumRelayDeploymentState.Unknown:
                    return QuantumRelayOperationalState.HardwareFault;
            }

            if (requiresCommNetHardware && !hasCommNetHardware)
                return QuantumRelayOperationalState.NoCommNetHardware;

            if (!hasPower)
                return QuantumRelayOperationalState.InsufficientPower;

            if (!isSynchronized)
                return QuantumRelayOperationalState.Synchronizing;

            return QuantumRelayOperationalState.Operational;
        }

        public static string GetDisplayName(
            QuantumRelayOperationalState state)
        {
            switch (state)
            {
                case QuantumRelayOperationalState.Disabled:
                    return "Disabled";
                case QuantumRelayOperationalState.Retracted:
                    return "Retracted";
                case QuantumRelayOperationalState.Deploying:
                    return "Deploying";
                case QuantumRelayOperationalState.Synchronizing:
                    return "Synchronizing";
                case QuantumRelayOperationalState.Operational:
                    return "Operational";
                case QuantumRelayOperationalState.InsufficientPower:
                    return "Insufficient Power";
                case QuantumRelayOperationalState.NoCommNetHardware:
                    return "No CommNet Hardware";
                case QuantumRelayOperationalState.Retracting:
                    return "Retracting";
                case QuantumRelayOperationalState.HardwareFault:
                    return "Hardware Fault";
                default:
                    return "Unknown";
            }
        }
    }
}
