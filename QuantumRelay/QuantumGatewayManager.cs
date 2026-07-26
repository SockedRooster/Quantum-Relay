using System;
using System.Collections.Generic;
using System.Reflection;
using CommNet;
using QuantumRelay.Core;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Maintains all active quantum gateway pairs. QuantumCommNetwork asks this
    /// manager whether any two CommNodes are joined by a powered wormhole link.
    /// </summary>
    internal static class QuantumGatewayManager
    {
        private sealed class GatewayPair
        {
            public string Id;
            public CommNode NodeA;
            public CommNode NodeB;
            public Guid VesselA;
            public Guid VesselB;
        }

        private static readonly List<GatewayPair> _pairs = new List<GatewayPair>();

        public static bool Active { get { return _pairs.Count > 0; } }
        public static int ActivePairCount { get { return _pairs.Count; } }

        public static void SetActiveLinks(IEnumerable<ActiveQuantumLink> links)
        {
            List<GatewayPair> next = new List<GatewayPair>();

            if (links != null)
            {
                foreach (ActiveQuantumLink link in links)
                {
                    if (link == null || !link.Online || !link.HasValidGateways)
                        continue;

                    CommNode nodeA = FindCommNode(link.GatewayA.Vessel);
                    CommNode nodeB = FindCommNode(link.GatewayB.Vessel);
                    if (nodeA == null || nodeB == null || ReferenceEquals(nodeA, nodeB))
                        continue;

                    next.Add(new GatewayPair
                    {
                        Id = link.Id,
                        NodeA = nodeA,
                        NodeB = nodeB,
                        VesselA = link.GatewayA.Vessel.id,
                        VesselB = link.GatewayB.Vessel.id
                    });
                }
            }

            bool changed = !SamePairs(_pairs, next);
            _pairs.Clear();
            _pairs.AddRange(next);

            if (!changed) return;

            Debug.Log("[QuantumRelay] Quantum graph updated | activeLinks=" + _pairs.Count);
            QueueRebuild();
        }

        public static void SetActive(GatewayCandidate a, GatewayCandidate b, bool active)
        {
            ActiveQuantumLink link = new ActiveQuantumLink
            {
                Id = "legacy",
                DisplayName = "Primary Quantum Link",
                GatewayA = a,
                GatewayB = b,
                Online = active,
                Reason = active ? "ready" : "offline"
            };
            SetActiveLinks(new[] { link });
        }

        public static bool IsGatewayPair(CommNode a, CommNode b)
        {
            if (a == null || b == null) return false;
            for (int i = 0; i < _pairs.Count; i++)
            {
                GatewayPair pair = _pairs[i];
                if ((ReferenceEquals(a, pair.NodeA) && ReferenceEquals(b, pair.NodeB)) ||
                    (ReferenceEquals(a, pair.NodeB) && ReferenceEquals(b, pair.NodeA)))
                    return true;
            }
            return false;
        }

        public static void Clear()
        {
            if (_pairs.Count == 0) return;
            _pairs.Clear();
            QueueRebuild();
        }

        public static void QueueRebuildNow() { QueueRebuild(); }

        private static bool SamePairs(List<GatewayPair> current, List<GatewayPair> next)
        {
            if (current.Count != next.Count) return false;
            for (int i = 0; i < current.Count; i++)
            {
                GatewayPair a = current[i];
                bool found = false;
                for (int j = 0; j < next.Count; j++)
                {
                    GatewayPair b = next[j];
                    if (string.Equals(a.Id, b.Id, StringComparison.Ordinal) &&
                        a.VesselA == b.VesselA && a.VesselB == b.VesselB)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        private static CommNode FindCommNode(Vessel vessel)
        {
            if (vessel == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            List<object> roots = new List<object>();
            try { if (vessel.connection != null) roots.Add(vessel.connection); } catch { }
            try
            {
                object modules = ReadMember(vessel, "vesselModules", flags) ?? ReadMember(vessel, "Modules", flags);
                System.Collections.IEnumerable enumerable = modules as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    foreach (object module in enumerable)
                        if (module != null && module.GetType().Name.IndexOf("CommNetVessel", StringComparison.OrdinalIgnoreCase) >= 0)
                            roots.Add(module);
                }
            }
            catch { }

            foreach (object root in roots)
            {
                CommNode found = FindNodeRecursive(root, flags, 0, new HashSet<object>(ReferenceComparer.Instance));
                if (found != null) return found;
            }
            return null;
        }

        private static CommNode FindNodeRecursive(object value, BindingFlags flags, int depth, HashSet<object> visited)
        {
            if (value == null || depth > 4 || visited.Contains(value)) return null;
            visited.Add(value);
            CommNode direct = value as CommNode;
            if (direct != null) return direct;
            foreach (string name in new[] { "Comm", "comm", "Node", "node", "CommNode", "commNode" })
            {
                object child = ReadMember(value, name, flags);
                if (child == null) continue;
                CommNode node = child as CommNode;
                if (node != null) return node;
                node = FindNodeRecursive(child, flags, depth + 1, visited);
                if (node != null) return node;
            }
            return null;
        }

        private static object ReadMember(object instance, string name, BindingFlags flags)
        {
            if (instance == null) return null;
            try
            {
                Type type = instance.GetType();
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(instance, null);
                FieldInfo field = type.GetField(name, flags);
                return field != null ? field.GetValue(instance) : null;
            }
            catch { return null; }
        }

        private static void QueueRebuild()
        {
            try { if (CommNetNetwork.Instance != null) CommNetNetwork.Instance.QueueRebuild(); }
            catch (Exception ex) { Debug.LogWarning("[QuantumRelay] Unable to queue CommNet rebuild: " + ex.Message); }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) { return ReferenceEquals(x, y); }
            public int GetHashCode(object obj) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj); }
        }
    }
}
