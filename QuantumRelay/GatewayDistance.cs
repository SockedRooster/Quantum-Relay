using System;

namespace QuantumRelay
{
    internal static class GatewayDistance
    {
        // Unloaded vessels may not have a current physics CoMD after loading a
        // save. Read their orbital state without loading or moving the vessel.
        public static bool TryMeasure(Vessel vessel, CelestialBody endpoint, out double distance)
        {
            distance = double.PositiveInfinity;
            if (vessel == null || endpoint == null) return false;
            try
            {
                if (vessel.loaded)
                    distance = Vector3d.Distance(vessel.GetWorldPos3D(), endpoint.position);
                else if (vessel.LandedOrSplashed)
                {
                    if (vessel.mainBody == null) return false;
                    distance = Vector3d.Distance(vessel.mainBody.GetWorldSurfacePosition(
                        vessel.latitude, vessel.longitude, vessel.altitude), endpoint.position);
                }
                else
                {
                    Orbit orbit = vessel.orbit;
                    if (orbit == null || orbit.referenceBody == null) return false;
                    double ut = Planetarium.GetUniversalTime();
                    // Avoid subtracting interstellar world coordinates when the
                    // vessel orbits the endpoint itself (precision loss).
                    distance = ReferenceEquals(orbit.referenceBody, endpoint)
                        ? orbit.getRelativePositionAtUT(ut).magnitude
                        : Vector3d.Distance(orbit.getPositionAtUT(ut), endpoint.position);
                }
                return !double.IsNaN(distance) && !double.IsInfinity(distance) && distance >= 0;
            }
            catch
            {
                // Startup data can be incomplete. The regular scan retries;
                // never substitute a stale physics position for an unknown orbit.
                return false;
            }
        }
    }
}
