namespace QuantumRelay
{
    internal static class QuantumRelaySettings
    {
        public const double GatewayRadiusMetres = 250000.0;
        public const double ElectricChargePerSecondPerGateway = 5.0;

        // Expensive work is deliberately slow and event-driven.
        public const double FullGatewayScanIntervalSeconds = 15.0;
        public const double DirtyScanDebounceSeconds = 1.0;

        // Only the two selected gateways are refreshed and powered at this rate.
        public const double GatewayMaintenanceIntervalSeconds = 1.0;
        public const double DetailedLogIntervalSeconds = 30.0;
        public const double EdgeRetryIntervalSeconds = 2.0;

        public const string WormholeTag = "Wormhole";
        public const string WormholeA = "KevbasAnomalyA";
        public const string WormholeB = "KevbasAnomalyB";
        public const string Rfl2000PartName = "nfex-antenna-reflector-huge-1";
        public const string FraRelayPartName = "nfex-antenna-feeder-relay-1";
        public const string ReflectorModuleName = "ModuleDeployableReflector";
        public const string FeedModuleName = "ModuleAntennaFeed";
        public const string TransmitterModuleName = "ModuleDataTransmitter";
        public const string CommandModuleName = "ModuleCommand";
        public const string ReflectorAnimationName = "AntennaExtend";
    }
}
