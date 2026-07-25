using System;
using System.Collections.Generic;
using System.Reflection;
using CommNet;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Holds the two currently powered gateway CommNodes. QuantumCommNetwork
    /// consults this state while the stock graph is being rebuilt.
    /// </summary>
    internal static class QuantumGatewayManager
    {
        private static CommNode _nodeA;
        private static CommNode _nodeB;
        private static Guid _vesselA;
        private static Guid _vesselB;
        private static bool _active;

        public static bool Active => _active && _nodeA != null && _nodeB != null;

        public static void SetActive(GatewayCandidate a, GatewayCandidate b, bool active)
        {
            Guid nextA = a?.Vessel != null ? a.Vessel.id : Guid.Empty;
            Guid nextB = b?.Vessel != null ? b.Vessel.id : Guid.Empty;
            CommNode nextNodeA = active ? FindCommNode(a?.Vessel) : null;
            CommNode nextNodeB = active ? FindCommNode(b?.Vessel) : null;
            bool nextActive = active && nextNodeA != null && nextNodeB != null && !ReferenceEquals(nextNodeA, nextNodeB);

            bool changed = _active != nextActive ||
                           _vesselA != nextA ||
                           _vesselB != nextB ||
                           !ReferenceEquals(_nodeA, nextNodeA) ||
                           !ReferenceEquals(_nodeB, nextNodeB);

            _active = nextActive;
            _vesselA = nextA;
            _vesselB = nextB;
            _nodeA = nextNodeA;
            _nodeB = nextNodeB;

            if (!changed) return;

            Debug.Log("[QuantumRelay] Gateway graph state changed | active=" + Active +
                      " | nodeA=" + Describe(_nodeA) + " | nodeB=" + Describe(_nodeB));
            QueueRebuild();
        }

        public static bool IsGatewayPair(CommNode a, CommNode b)
        {
            if (!Active || a == null || b == null) return false;
            return (ReferenceEquals(a, _nodeA) && ReferenceEquals(b, _nodeB)) ||
                   (ReferenceEquals(a, _nodeB) && ReferenceEquals(b, _nodeA));
        }

        public static void Clear()
        {
            bool changed = _active || _nodeA != null || _nodeB != null;
            _active = false;
            _nodeA = null;
            _nodeB = null;
            _vesselA = Guid.Empty;
            _vesselB = Guid.Empty;
            if (changed) QueueRebuild();
        }

        private static CommNode FindCommNode(Vessel vessel)
        {
            if (vessel == null) return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var roots = new List<object>();
            try
            {
                if (vessel.connection != null) roots.Add(vessel.connection);
            }
            catch { }

            try
            {
                object modules = ReadMember(vessel, "vesselModules", flags) ?? ReadMember(vessel, "Modules", flags);
                var enumerable = modules as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    foreach (object module in enumerable)
                    {
                        if (module != null && module.GetType().Name.IndexOf("CommNetVessel", StringComparison.OrdinalIgnoreCase) >= 0)
                            roots.Add(module);
                    }
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
            try
            {
                if (CommNetNetwork.Instance != null)
                    CommNetNetwork.Instance.QueueRebuild();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QuantumRelay] Unable to queue CommNet rebuild: " + ex.Message);
            }
        }

        private static string Describe(object value)
        {
            return value == null ? "null" : value.GetType().FullName;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) { return ReferenceEquals(x, y); }
            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
