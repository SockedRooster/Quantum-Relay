using System;
using System.Collections.Generic;
using System.Linq;
using QuantumRelay.Models;

namespace QuantumRelay.Core
{
    /// <summary>
    /// Calculates quantum routes from the current runtime link snapshot.
    /// This first implementation deliberately does not modify CommNet.
    /// </summary>
    internal sealed class QuantumRouter
    {
        private readonly QuantumRouteCache _cache;

        public QuantumRouter(QuantumRouteCache cache)
        {
            if (cache == null) throw new ArgumentNullException("cache");
            _cache = cache;
        }

        public QuantumRoute FindRoute(
            Vessel source,
            Vessel destination,
            IEnumerable<ActiveQuantumLink> links,
            double universalTime)
        {
            if (source == null || destination == null)
            {
                return QuantumRoute.Invalid(
                    new RouteKey(Guid.Empty, Guid.Empty),
                    "source or destination vessel is missing",
                    universalTime);
            }

            RouteKey key = new RouteKey(source.id, destination.id);
            QuantumRoute cached;
            if (_cache.TryGet(key, universalTime, out cached)) return cached;

            QuantumRoute route = CalculateRoute(
                key,
                source,
                destination,
                links,
                universalTime);

            _cache.Store(route);
            return route;
        }

        public void InvalidateAll()
        {
            _cache.Clear();
        }

        private static QuantumRoute CalculateRoute(
            RouteKey key,
            Vessel source,
            Vessel destination,
            IEnumerable<ActiveQuantumLink> links,
            double universalTime)
        {
            List<ActiveQuantumLink> onlineLinks = links == null
                ? new List<ActiveQuantumLink>()
                : links.Where(IsUsable).ToList();

            if (onlineLinks.Count == 0)
            {
                return QuantumRoute.Invalid(key, "no online quantum networks", universalTime);
            }

            ActiveQuantumLink direct = onlineLinks.FirstOrDefault(
                link => ContainsVessel(link, source.id) &&
                        ContainsVessel(link, destination.id));

            if (direct != null)
            {
                return QuantumRoute.Valid(
                    key,
                    QuantumRouteType.SameNetwork,
                    new[] { direct },
                    LinkSynchronization(direct),
                    universalTime);
            }

            ActiveQuantumLink sourceLink = onlineLinks.FirstOrDefault(
                link => ContainsVessel(link, source.id));
            ActiveQuantumLink destinationLink = onlineLinks.FirstOrDefault(
                link => ContainsVessel(link, destination.id));

            if (sourceLink == null || destinationLink == null)
            {
                return QuantumRoute.Invalid(
                    key,
                    "one or both vessels are not registered quantum gateways",
                    universalTime);
            }

            if (string.Equals(
                sourceLink.NetworkId,
                destinationLink.NetworkId,
                StringComparison.OrdinalIgnoreCase))
            {
                return QuantumRoute.Valid(
                    key,
                    QuantumRouteType.SameNetwork,
                    new[] { sourceLink },
                    LinkSynchronization(sourceLink),
                    universalTime);
            }

            // Cross-network routes are represented here but are not injected
            // into CommNet until the integration sprint. The ordinary CommNet
            // transfer between the two Kerbol-side entrances remains required.
            return QuantumRoute.Valid(
                key,
                QuantumRouteType.CrossNetwork,
                new[] { sourceLink, destinationLink },
                Math.Min(
                    LinkSynchronization(sourceLink),
                    LinkSynchronization(destinationLink)),
                universalTime);
        }

        private static bool IsUsable(ActiveQuantumLink link)
        {
            return link != null && link.Online && link.HasValidGateways;
        }

        private static bool ContainsVessel(ActiveQuantumLink link, Guid vesselId)
        {
            return GatewayMatches(link.GatewayA, vesselId) ||
                   GatewayMatches(link.GatewayB, vesselId);
        }

        private static bool GatewayMatches(GatewayCandidate gateway, Guid vesselId)
        {
            return gateway != null &&
                   gateway.Vessel != null &&
                   gateway.Vessel.id == vesselId;
        }

        private static double LinkSynchronization(ActiveQuantumLink link)
        {
            if (link == null) return 0.0;

            double a = link.GatewayA == null
                ? 0.0
                : link.GatewayA.RelaySynchronizationFraction;
            double b = link.GatewayB == null
                ? 0.0
                : link.GatewayB.RelaySynchronizationFraction;

            return Math.Min(a, b);
        }
    }
}
