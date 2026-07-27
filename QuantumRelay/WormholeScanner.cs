using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace QuantumRelay
{
    internal sealed class WormholeInfo
    {
        public CelestialBody Body { get; set; }
        public CelestialBody Partner { get; set; }
        public string Name => Body != null ? Body.bodyName : "<null>";
        public string PartnerName => Partner != null ? Partner.bodyName : "<unknown>";
    }

    /// <summary>
    /// Discovers wormhole endpoint bodies.
    ///
    /// Endpoints explicitly named by QUANTUM_BRIDGE configuration are treated
    /// as authoritative. This is required for Promised Worlds endpoints such as
    /// BorgalsAnomalyA/B, which are valid wormholes but are not guaranteed to
    /// expose the same body tag or partner metadata as KevbasAnomalyA/B.
    /// </summary>
    internal static class WormholeScanner
    {
        private static readonly HashSet<string> loggedConfiguredEndpoints =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> loggedMissingEndpoints =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static List<WormholeInfo> FindAll()
        {
            List<WormholeInfo> results = new List<WormholeInfo>();
            if (FlightGlobals.Bodies == null)
                return results;

            HashSet<string> configuredEndpoints =
                GetConfiguredBridgeEndpoints();

            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (body == null)
                    continue;

                bool explicitlyConfigured =
                    configuredEndpoints.Contains(body.bodyName);

                if (!explicitlyConfigured && !IsWormhole(body))
                    continue;

                CelestialBody partner =
                    TryReadPartner(body, configuredEndpoints);

                results.Add(
                    new WormholeInfo
                    {
                        Body = body,
                        Partner = partner
                    });

                if (explicitlyConfigured &&
                    loggedConfiguredEndpoints.Add(body.bodyName))
                {
                    Debug.Log(
                        "[QuantumRelay] Configured wormhole endpoint discovered | body=" +
                        body.bodyName +
                        " | partner=" +
                        (partner != null ? partner.bodyName : "metadata unavailable"));
                }
            }

            foreach (string endpointName in configuredEndpoints)
            {
                bool found = results.Any(
                    wormhole =>
                        wormhole != null &&
                        string.Equals(
                            wormhole.Name,
                            endpointName,
                            StringComparison.OrdinalIgnoreCase));

                if (!found && loggedMissingEndpoints.Add(endpointName))
                {
                    Debug.LogWarning(
                        "[QuantumRelay] Configured wormhole endpoint was not found in FlightGlobals.Bodies | body=" +
                        endpointName);
                }
            }

            return results
                .OrderBy(wormhole => wormhole.Name)
                .ToList();
        }

        private static HashSet<string> GetConfiguredBridgeEndpoints()
        {
            HashSet<string> endpoints =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddEndpoint(endpoints, QuantumRelaySettings.WormholeA);
            AddEndpoint(endpoints, QuantumRelaySettings.WormholeB);

            try
            {
                ConfigNode[] bridgeNodes =
                    GameDatabase.Instance.GetConfigNodes("QUANTUM_BRIDGE");

                foreach (ConfigNode node in bridgeNodes)
                {
                    if (node == null)
                        continue;

                    AddEndpoint(endpoints, node.GetValue("gatewayA"));
                    AddEndpoint(endpoints, node.GetValue("gatewayB"));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Bridge endpoint inspection failed: " +
                    ex.Message);
            }

            return endpoints;
        }

        private static void AddEndpoint(
            HashSet<string> endpoints,
            string endpointName)
        {
            if (!string.IsNullOrWhiteSpace(endpointName))
                endpoints.Add(endpointName.Trim());
        }

        private static bool IsWormhole(CelestialBody body)
        {
            if (string.Equals(
                    body.bodyName,
                    QuantumRelaySettings.WormholeA,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    body.bodyName,
                    QuantumRelaySettings.WormholeB,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                foreach (ConfigNode node in
                         GameDatabase.Instance.GetConfigNodes("Body"))
                {
                    if (!string.Equals(
                            node.GetValue("name"),
                            body.bodyName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string tag =
                        node.GetValue("Tag") ?? node.GetValue("tag");

                    if (string.Equals(
                            tag,
                            QuantumRelaySettings.WormholeTag,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Body-tag inspection failed: " +
                    ex.Message);
            }

            return false;
        }

        private static CelestialBody TryReadPartner(
            CelestialBody body,
            HashSet<string> configuredEndpoints)
        {
            CelestialBody reflectedPartner =
                TryReadPartnerFromKopernicusExpansion(body);

            if (reflectedPartner != null)
                return reflectedPartner;

            string configuredPartnerName =
                FindConfiguredPartnerName(
                    body != null ? body.bodyName : null);

            if (!string.IsNullOrEmpty(configuredPartnerName))
            {
                CelestialBody configuredPartner =
                    FindBody(configuredPartnerName);

                if (configuredPartner != null)
                    return configuredPartner;
            }

            string expected =
                string.Equals(
                    body.bodyName,
                    QuantumRelaySettings.WormholeA,
                    StringComparison.OrdinalIgnoreCase)
                    ? QuantumRelaySettings.WormholeB
                    : string.Equals(
                        body.bodyName,
                        QuantumRelaySettings.WormholeB,
                        StringComparison.OrdinalIgnoreCase)
                        ? QuantumRelaySettings.WormholeA
                        : null;

            if (!string.IsNullOrEmpty(expected))
                return FindBody(expected);

            // The set is passed deliberately so future endpoint-resolution
            // strategies can remain constrained to configured wormholes.
            // Referencing it here also makes that intent explicit.
            if (configuredEndpoints == null || configuredEndpoints.Count == 0)
                return null;

            return null;
        }

        private static CelestialBody
            TryReadPartnerFromKopernicusExpansion(CelestialBody body)
        {
            try
            {
                Type componentType =
                    AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(SafeGetTypes)
                        .FirstOrDefault(
                            type =>
                                type.FullName ==
                                "KopernicusExpansion.Wormholes.WormholeComponent");

                if (componentType != null && body.scaledBody != null)
                {
                    object component =
                        body.scaledBody.GetComponent(componentType);

                    if (component != null)
                    {
                        const BindingFlags flags =
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.Instance;

                        foreach (string name in
                                 new[]
                                 {
                                     "partnerBody",
                                     "PartnerBody",
                                     "partner",
                                     "_partner"
                                 })
                        {
                            PropertyInfo property =
                                componentType.GetProperty(name, flags);

                            if (property != null &&
                                property.GetValue(component, null)
                                    is CelestialBody propertyPartner)
                            {
                                return propertyPartner;
                            }

                            FieldInfo field =
                                componentType.GetField(name, flags);

                            if (field != null &&
                                field.GetValue(component)
                                    is CelestialBody fieldPartner)
                            {
                                return fieldPartner;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[QuantumRelay] KEX partner reflection failed for " +
                    body.bodyName +
                    ": " +
                    ex.Message);
            }

            return null;
        }

        private static string FindConfiguredPartnerName(
            string endpointName)
        {
            if (string.IsNullOrEmpty(endpointName))
                return null;

            try
            {
                ConfigNode[] bridgeNodes =
                    GameDatabase.Instance.GetConfigNodes("QUANTUM_BRIDGE");

                foreach (ConfigNode node in bridgeNodes)
                {
                    if (node == null)
                        continue;

                    string gatewayA = node.GetValue("gatewayA");
                    string gatewayB = node.GetValue("gatewayB");

                    if (string.Equals(
                            endpointName,
                            gatewayA,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return gatewayB;
                    }

                    if (string.Equals(
                            endpointName,
                            gatewayB,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return gatewayA;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Configured partner lookup failed for " +
                    endpointName +
                    ": " +
                    ex.Message);
            }

            return null;
        }

        private static CelestialBody FindBody(string bodyName)
        {
            if (string.IsNullOrEmpty(bodyName) ||
                FlightGlobals.Bodies == null)
            {
                return null;
            }

            return FlightGlobals.Bodies.FirstOrDefault(
                body =>
                    body != null &&
                    string.Equals(
                        body.bodyName,
                        bodyName,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<Type> SafeGetTypes(
            Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }
    }
}
