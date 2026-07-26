namespace QuantumRelay
{
    public enum QuantumRelayDeploymentState
    {
        Fixed,
        Missing,
        Retracted,
        Extending,
        Extended,
        Retracting,
        Broken,
        Unknown
    }

    public enum QuantumRelayOperationalState
    {
        Disabled,
        Retracted,
        Deploying,
        Synchronizing,
        Operational,
        InsufficientPower,
        NoCommNetHardware,
        Retracting,
        HardwareFault,
        Unknown
    }
}
