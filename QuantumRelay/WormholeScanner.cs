using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace QuantumRelay
{
    internal sealed class WormholeInfo
    {
        public CelestialBody Body { get; set; }
        public CelestialBody Partner { get; set; }
        public string Name => Body != null ? Body.bodyName : "<null>";
        public string PartnerName => Partner != null ? Partner.bodyName : "<unknown>";
    }

    internal static class WormholeScanner
    {
        public static List<WormholeInfo> FindAll()
        {
            var results = new List<WormholeInfo>();
            if (FlightGlobals.Bodies == null) return results;

            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (body == null || !IsWormhole(body)) continue;
                results.Add(new WormholeInfo { Body = body, Partner = TryReadPartner(body) });
            }

            return results.OrderBy(w => w.Name).ToList();
        }

        private static bool IsWormhole(CelestialBody body)
        {
            if (string.Equals(body.bodyName, QuantumRelaySettings.WormholeA, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(body.bodyName, QuantumRelaySettings.WormholeB, StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("Body"))
                {
                    if (!string.Equals(node.GetValue("name"), body.bodyName, StringComparison.OrdinalIgnoreCase)) continue;
                    string tag = node.GetValue("Tag") ?? node.GetValue("tag");
                    if (string.Equals(tag, QuantumRelaySettings.WormholeTag, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[QuantumRelay] Body-tag inspection failed: " + ex.Message);
            }
            return false;
        }

        private static CelestialBody TryReadPartner(CelestialBody body)
        {
            try
            {
                Type componentType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(SafeGetTypes)
                    .FirstOrDefault(t => t.FullName == "KopernicusExpansion.Wormholes.WormholeComponent");

                if (componentType != null && body.scaledBody != null)
                {
                    object component = body.scaledBody.GetComponent(componentType);
                    if (component != null)
                    {
                        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                        foreach (string name in new[] { "partnerBody", "PartnerBody", "partner", "_partner" })
                        {
                            PropertyInfo property = componentType.GetProperty(name, flags);
                            if (property != null && property.GetValue(component, null) is CelestialBody p) return p;
                            FieldInfo field = componentType.GetField(name, flags);
                            if (field != null && field.GetValue(component) is CelestialBody f) return f;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[QuantumRelay] KEX partner reflection failed for " + body.bodyName + ": " + ex.Message);
            }

            string expected = string.Equals(body.bodyName, QuantumRelaySettings.WormholeA, StringComparison.OrdinalIgnoreCase)
                ? QuantumRelaySettings.WormholeB
                : string.Equals(body.bodyName, QuantumRelaySettings.WormholeB, StringComparison.OrdinalIgnoreCase)
                    ? QuantumRelaySettings.WormholeA : null;
            return expected == null ? null : FlightGlobals.GetBodyByName(expected);
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
            catch { return Enumerable.Empty<Type>(); }
        }
    }
}
