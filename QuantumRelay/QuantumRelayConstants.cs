namespace QuantumRelay
{
    /// <summary>Shared identifiers and defaults used throughout Quantum Relay.</summary>
    internal static class QuantumRelayConstants
    {
        public const string Version = "1.4.0-beta1";
        public const string DisplayVersion = "1.4.0 Beta 1";

        public const int DefaultSignalQualityPercent = 100;
        public const double DefaultGatewayRadiusMetres = 250000.0;
        public const double DefaultElectricChargePerSecondPerGateway = 5.0;
        public const string WormholeTag = "Wormhole";
        public const string WormholeA = "KevbasAnomalyA";
        public const string WormholeB = "KevbasAnomalyB";
        public const string QuantumRelayModuleName = "ModuleQuantumRelay";
        public const string LegacyReflectorModuleName = "ModuleDeployableReflector";
        public const string TransmitterModuleName = "ModuleDataTransmitter";
        public const string CommandModuleName = "ModuleCommand";
        public const string ReflectorAnimationName = "AntennaExtend";
    }
}
