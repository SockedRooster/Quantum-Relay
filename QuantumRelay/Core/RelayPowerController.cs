using System;

namespace QuantumRelay
{
    internal sealed class RelayPowerController
    {
        private const double MinimumReceivedFraction = 0.999;
        private const double SmallAmount = 0.0001;

        public double CurrentRate { get; private set; }
        public bool HasPower { get; private set; } = true;

        public double GetRequiredRate(
            QuantumRelayOperationalState state,
            bool relayEnabled,
            double idleRate,
            double synchronizationRate,
            double operationalRate)
        {
            switch (state)
            {
                case QuantumRelayOperationalState.Synchronizing:
                    return Math.Max(0.0, synchronizationRate);

                case QuantumRelayOperationalState.Operational:
                    return Math.Max(0.0, operationalRate);

                case QuantumRelayOperationalState.Deploying:
                case QuantumRelayOperationalState.Retracting:
                case QuantumRelayOperationalState.Retracted:
                case QuantumRelayOperationalState.NoCommNetHardware:
                case QuantumRelayOperationalState.HardwareFault:
                    return relayEnabled ? Math.Max(0.0, idleRate) : 0.0;

                default:
                    return 0.0;
            }
        }

        public bool Consume(Part part, double rate, double deltaTime)
        {
            CurrentRate = Math.Max(0.0, rate);

            if (part == null || CurrentRate <= 0.0 || deltaTime <= 0.0)
            {
                HasPower = true;
                return true;
            }

            double requested = CurrentRate * deltaTime;
            double received = part.RequestResource(
                PartResourceLibrary.ElectricityHashcode,
                requested);

            HasPower =
                requested <= SmallAmount ||
                received / requested >= MinimumReceivedFraction;

            return HasPower;
        }
    }
}
