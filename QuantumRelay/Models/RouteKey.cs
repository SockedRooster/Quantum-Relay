using System;

namespace QuantumRelay.Models
{
    /// <summary>
    /// Identifies one directional route request between two vessels.
    /// </summary>
    internal struct RouteKey : IEquatable<RouteKey>
    {
        public Guid SourceVesselId { get; private set; }
        public Guid DestinationVesselId { get; private set; }

        public RouteKey(Guid sourceVesselId, Guid destinationVesselId)
        {
            SourceVesselId = sourceVesselId;
            DestinationVesselId = destinationVesselId;
        }

        public bool Equals(RouteKey other)
        {
            return SourceVesselId.Equals(other.SourceVesselId) &&
                   DestinationVesselId.Equals(other.DestinationVesselId);
        }

        public override bool Equals(object obj)
        {
            return obj is RouteKey && Equals((RouteKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SourceVesselId.GetHashCode() * 397) ^
                       DestinationVesselId.GetHashCode();
            }
        }

        public override string ToString()
        {
            return SourceVesselId.ToString("N") + "->" +
                   DestinationVesselId.ToString("N");
        }
    }
}
