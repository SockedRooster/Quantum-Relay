using System;

namespace QuantumRelay
{
    /// <summary>
    /// Handles the gateway operating cost without fighting KSP's background
    /// resource simulation. Loaded vessels pay the configured EC cost through
    /// the stock RequestResource pipeline. Unloaded vessels are only checked
    /// for an EC reserve; their proto resources are never edited directly.
    /// </summary>
    internal static class PowerManager
    {
        public static bool Consume(GatewayCandidate gateway, double amount)
        {
            if (gateway?.Vessel == null) return false;

            // A configured draw of 0 EC/s disables the power requirement.
            // Do not call RequestResource with a zero amount or interpret the
            // resulting zero draw as a power failure.
            if (amount <= 0.0) return true;
            if (gateway.ElectricChargeAmount + 1e-6 < amount) return false;

            if (gateway.Vessel.loaded)
            {
                Part requester = gateway.Vessel.rootPart;
                if (requester == null) return false;

                double taken = requester.RequestResource("ElectricCharge", amount);
                gateway.ElectricChargeAmount = Math.Max(0.0, gateway.ElectricChargeAmount - taken);
                return taken >= amount * 0.999;
            }

            // Do not subtract directly from ProtoPartResourceSnapshot. KSP does
            // not continuously run solar-panel generation for every unloaded
            // vessel, so direct proto deductions cause a gateway to drain while
            // it is off-screen even when its panels are capable of covering the
            // relay load. The unloaded endpoint remains eligible while it has a
            // usable EC reserve. Once loaded, normal stock resource flow applies.
            return gateway.ElectricChargeAmount >= amount;
        }
    }
}
