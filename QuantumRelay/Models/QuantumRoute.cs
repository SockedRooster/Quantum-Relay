using System;
using System.Collections.Generic;
using QuantumRelay.Core;

namespace QuantumRelay.Models
{
    internal enum QuantumRouteType
    {
        None = 0,
        SameNetwork = 1,
        CrossNetwork = 2
    }

    /// <summary>
    /// Immutable description of a route calculated by the quantum router.
    /// Sprint 1 exposes route state without changing stock CommNet behaviour.
    /// </summary>
    internal sealed class QuantumRoute
    {
        private readonly List<ActiveQuantumLink> _links;

        public RouteKey Key { get; private set; }
        public QuantumRouteType RouteType { get; private set; }
        public bool IsValid { get; private set; }
        public string Reason { get; private set; }
        public double SynchronizationFraction { get; private set; }
        public double CreatedUniversalTime { get; private set; }
        public IList<ActiveQuantumLink> Links { get { return _links.AsReadOnly(); } }

        public ActiveQuantumLink SourceLink
        {
            get { return _links.Count > 0 ? _links[0] : null; }
        }

        public ActiveQuantumLink DestinationLink
        {
            get { return _links.Count > 1 ? _links[_links.Count - 1] : SourceLink; }
        }

        private QuantumRoute(
            RouteKey key,
            QuantumRouteType routeType,
            bool isValid,
            string reason,
            double synchronizationFraction,
            double createdUniversalTime,
            IEnumerable<ActiveQuantumLink> links)
        {
            Key = key;
            RouteType = routeType;
            IsValid = isValid;
            Reason = string.IsNullOrEmpty(reason) ? "unknown" : reason;
            SynchronizationFraction = Clamp01(synchronizationFraction);
            CreatedUniversalTime = createdUniversalTime;
            _links = links == null
                ? new List<ActiveQuantumLink>()
                : new List<ActiveQuantumLink>(links);
        }

        public static QuantumRoute Valid(
            RouteKey key,
            QuantumRouteType routeType,
            IEnumerable<ActiveQuantumLink> links,
            double synchronizationFraction,
            double createdUniversalTime)
        {
            return new QuantumRoute(
                key,
                routeType,
                true,
                "ready",
                synchronizationFraction,
                createdUniversalTime,
                links);
        }

        public static QuantumRoute Invalid(
            RouteKey key,
            string reason,
            double createdUniversalTime)
        {
            return new QuantumRoute(
                key,
                QuantumRouteType.None,
                false,
                reason,
                0.0,
                createdUniversalTime,
                null);
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0) return 0.0;
            if (value > 1.0) return 1.0;
            return value;
        }
    }
}
