using System;
using System.Collections.Generic;
using QuantumRelay.Models;
using UnityEngine;

namespace QuantumRelay.Core
{
    /// <summary>
    /// Authoritative owner of Quantum Relay runtime services and registered
    /// relay modules for the current save.
    /// </summary>
    internal sealed class QuantumManager
    {
        private static readonly QuantumManager _instance = new QuantumManager();

        private readonly QuantumRouteCache _routeCache;
        private readonly QuantumRouter _router;
        private readonly HashSet<ModuleQuantumRelay> _registeredRelays;

        private bool _initialized;
        private string _owner = "unowned";

        public static QuantumManager Instance { get { return _instance; } }
        public QuantumRouteCache RouteCache { get { return _routeCache; } }
        public QuantumRouter Router { get { return _router; } }
        public bool IsInitialized { get { return _initialized; } }
        public int RegisteredRelayCount { get { return _registeredRelays.Count; } }

        private QuantumManager()
        {
            _routeCache = new QuantumRouteCache();
            _router = new QuantumRouter(_routeCache);
            _registeredRelays = new HashSet<ModuleQuantumRelay>();
        }

        /// <summary>
        /// Starts the save-scoped Quantum Relay services. This method is
        /// intentionally idempotent because KSP can recreate scene objects
        /// while retaining the same scenario module.
        /// </summary>
        public void Initialize(string owner)
        {
            string requestedOwner = string.IsNullOrEmpty(owner)
                ? "unknown"
                : owner;

            if (_initialized)
            {
                _owner = requestedOwner;
                return;
            }

            _initialized = true;
            _owner = requestedOwner;
            _router.InvalidateAll();

            Debug.Log(
                "[QuantumRelay] QuantumManager online" +
                " | owner=" + _owner +
                " | routeCache=ready" +
                " | router=ready");
        }

        /// <summary>
        /// Stops save-scoped services and releases references to loaded parts.
        /// </summary>
        public void Shutdown(string reason)
        {
            if (!_initialized && _registeredRelays.Count == 0)
                return;

            int released = _registeredRelays.Count;
            _registeredRelays.Clear();
            _router.InvalidateAll();
            _initialized = false;
            _owner = "unowned";

            Debug.Log(
                "[QuantumRelay] QuantumManager offline" +
                " | reason=" +
                (string.IsNullOrEmpty(reason) ? "unspecified" : reason) +
                " | releasedRelays=" + released);
        }

        /// <summary>
        /// Registers a loaded relay module with the save-scoped manager.
        /// Duplicate registration is ignored.
        /// </summary>
        public void RegisterRelay(ModuleQuantumRelay relay)
        {
            if (relay == null)
                return;

            EnsureInitialized("relay registration fallback");

            if (!_registeredRelays.Add(relay))
                return;

            _router.InvalidateAll();

            Debug.Log(
                "[QuantumRelay] Relay registered" +
                " | vessel=" + SafeVesselName(relay) +
                " | part=" + SafePartName(relay) +
                " | registered=" + _registeredRelays.Count);
        }

        /// <summary>
        /// Removes a relay module when its part or vessel is destroyed/unloaded.
        /// </summary>
        public void UnregisterRelay(ModuleQuantumRelay relay)
        {
            if (relay == null || !_registeredRelays.Remove(relay))
                return;

            _router.InvalidateAll();

            Debug.Log(
                "[QuantumRelay] Relay unregistered" +
                " | vessel=" + SafeVesselName(relay) +
                " | part=" + SafePartName(relay) +
                " | registered=" + _registeredRelays.Count);
        }

        /// <summary>
        /// Invalidates routing when a registered relay changes operational state.
        /// </summary>
        public void NotifyRelayStateChanged(ModuleQuantumRelay relay)
        {
            if (relay != null && !_registeredRelays.Contains(relay))
                RegisterRelay(relay);

            _router.InvalidateAll();
        }

        public QuantumRoute FindRoute(Vessel source, Vessel destination)
        {
            EnsureInitialized("route request fallback");

            return _router.FindRoute(
                source,
                destination,
                QuantumRelayRuntimeState.Links,
                GetUniversalTime());
        }

        public void NotifyTopologyChanged()
        {
            _router.InvalidateAll();
        }

        private void EnsureInitialized(string owner)
        {
            if (!_initialized)
                Initialize(owner);
        }

        private static string SafeVesselName(ModuleQuantumRelay relay)
        {
            try
            {
                return relay.part != null && relay.part.vessel != null
                    ? relay.part.vessel.vesselName
                    : "unassigned";
            }
            catch
            {
                return "unavailable";
            }
        }

        private static string SafePartName(ModuleQuantumRelay relay)
        {
            try
            {
                if (relay.part == null)
                    return "unassigned";

                if (relay.part.partInfo != null &&
                    !string.IsNullOrEmpty(relay.part.partInfo.name))
                {
                    return relay.part.partInfo.name;
                }

                return relay.part.name ?? "unnamed";
            }
            catch
            {
                return "unavailable";
            }
        }

        private static double GetUniversalTime()
        {
            try { return Planetarium.GetUniversalTime(); }
            catch { return 0.0; }
        }
    }
}
