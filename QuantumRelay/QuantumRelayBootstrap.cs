using System;
using System.Collections.Generic;
using System.Linq;
using QuantumRelay.Configuration;
using QuantumRelay.Core;
using UnityEngine;

namespace QuantumRelay
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal sealed class QuantumRelayBootstrap : MonoBehaviour
    {
        private readonly Dictionary<string, string> _lastStatus =
            new Dictionary<string, string>();

        private readonly HashSet<Guid> _hardwareDumped =
            new HashSet<Guid>();

        private List<WormholeInfo> _wormholes =
            new List<WormholeInfo>();

        private List<GatewayCandidate> _candidateCache =
            new List<GatewayCandidate>();

        private List<QuantumBridge> _bridgeDefinitions =
            new List<QuantumBridge>();

        private readonly List<ActiveQuantumLink> _links =
            new List<ActiveQuantumLink>();

        private float _nextFullScanTime;
        private float _nextMaintenanceTime;
        private float _nextDetailedLogTime;
        private float _dirtyAfterTime;
        private double _lastMaintenanceUt;
        private bool _cacheDirty = true;
        private int _lastOnlineCount = -1;
        private bool _quantumRelayEventsRegistered;

        public void Start()
        {
            Debug.Log("[QuantumRelay] Flight bootstrap starting.");
            QuantumRelaySettings.Load();
            _bridgeDefinitions = BridgeLoader.Load();
            _lastMaintenanceUt = Planetarium.GetUniversalTime();
            RegisterEvents();
            CommNetNetworkInstaller.EnsureInstalled();

            Debug.Log(
                "[QuantumRelay] Quantum Relay " +
                QuantumRelayConstants.DisplayVersion +
                " loaded | capacity-aware multi-wormhole network active");
        }

        public void OnDestroy()
        {
            UnregisterEvents();
            QuantumGatewayManager.Clear();
            QuantumRelayRuntimeState.Clear();
            Debug.Log("[QuantumRelay] Flight bootstrap destroyed.");
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            CommNetNetworkInstaller.EnsureInstalled();
            float realtime = Time.realtimeSinceStartup;

            if (QuantumRelayCommands.ConsumeRefresh())
            {
                _cacheDirty = true;
                _dirtyAfterTime = realtime;
                QuantumRelayRuntimeState.SetTicker(
                    "Gateway scan requested.");
            }

            if (QuantumRelayCommands.ConsumeRebuild())
            {
                QuantumGatewayManager.QueueRebuildNow();
                QuantumRelayRuntimeState.SetTicker(
                    "CommNet rebuild requested.");
            }

            if (realtime >= _nextFullScanTime ||
                (_cacheDirty && realtime >= _dirtyAfterTime))
            {
                RebuildGatewayCache(realtime);
            }

            if (realtime < _nextMaintenanceTime)
                return;

            _nextMaintenanceTime =
                realtime +
                (float)QuantumRelaySettings
                    .GatewayMaintenanceIntervalSeconds;

            MaintainLinks();
        }

        private void RebuildGatewayCache(float realtime)
        {
            _cacheDirty = false;
            _nextFullScanTime =
                realtime +
                (float)QuantumRelaySettings
                    .FullGatewayScanIntervalSeconds;

            _wormholes = WormholeScanner.FindAll();

            if (_wormholes == null || _wormholes.Count < 2)
            {
                _candidateCache.Clear();
                _links.Clear();
                PublishLinks(
                    "fewer than two wormholes detected");
                return;
            }

            _candidateCache =
                GatewayScanner.FindCandidates(_wormholes);

            BuildLinks();
            LogCandidates(realtime);
        }

        /// <summary>
        /// Reconciles bridge definitions against the current gateway inventory.
        ///
        /// Unlike v1.3.1, a gateway vessel is no longer globally reserved after
        /// one bridge. Each vessel has a capacity derived from its relay tier:
        /// legacy/QR-100=1, QR-250=2, QR-500=4, QR-750=6.
        ///
        /// Existing healthy endpoint selections are preferred so routine scans
        /// do not tear down or reshuffle working links.
        /// </summary>
        private void BuildLinks()
        {
            List<QuantumBridge> definitions =
                GetEffectiveBridgeDefinitions();

            List<ActiveQuantumLink> next =
                new List<ActiveQuantumLink>();

            Dictionary<Guid, int> gatewayUsage =
                new Dictionary<Guid, int>();

            foreach (QuantumBridge definition in definitions)
            {
                if (definition == null || !definition.Enabled)
                    continue;

                string id =
                    string.IsNullOrEmpty(definition.Name)
                        ? definition.GatewayA +
                          "::" +
                          definition.GatewayB
                        : definition.Name;

                ActiveQuantumLink existing =
                    _links.FirstOrDefault(
                        existingLink =>
                            existingLink != null &&
                            string.Equals(
                                existingLink.Id,
                                id,
                                StringComparison.Ordinal));

                WormholeInfo endA =
                    FindWormhole(definition.GatewayA);

                WormholeInfo endB =
                    FindWormhole(definition.GatewayB);

                GatewayCandidate selectedA =
                    SelectPreferredGateway(
                        _candidateCache,
                        endA,
                        gatewayUsage,
                        existing != null
                            ? existing.GatewayA
                            : null,
                        null);

                ReserveGateway(selectedA, gatewayUsage);

                GatewayCandidate selectedB =
                    SelectPreferredGateway(
                        _candidateCache,
                        endB,
                        gatewayUsage,
                        existing != null
                            ? existing.GatewayB
                            : null,
                        selectedA != null &&
                        selectedA.Vessel != null
                            ? selectedA.Vessel.id
                            : (Guid?)null);

                ReserveGateway(selectedB, gatewayUsage);

                ActiveQuantumLink link =
                    existing ?? new ActiveQuantumLink();

                bool sameGateways =
                    existing != null &&
                    SameGateway(
                        existing.GatewayA,
                        selectedA) &&
                    SameGateway(
                        existing.GatewayB,
                        selectedB);

                link.Id = id;
                link.DisplayName =
                    string.IsNullOrEmpty(
                        definition.DisplayName)
                        ? definition.Name
                        : definition.DisplayName;

                link.NetworkId =
                    string.IsNullOrEmpty(definition.NetworkId)
                        ? QuantumNetwork.CreateStableId(
                            definition.GatewayA,
                            definition.GatewayB)
                        : definition.NetworkId;

                link.GatewayA = selectedA;
                link.GatewayB = selectedB;

                // Preserve the online state of an unchanged link. New or
                // changed endpoints are validated during the normal maintenance
                // pass instead of being treated as immediately healthy.
                if (!sameGateways)
                {
                    link.Online = false;
                    link.Reason =
                        link.HasValidGateways
                            ? "validating new gateways"
                            : "missing valid gateway";
                }

                next.Add(link);
            }

            _links.Clear();
            _links.AddRange(next);

            PublishLinks(
                _links.Count == 0
                    ? "no bridge definitions"
                    : "capacity-aware gateway inventory reconciled");
        }

        private static GatewayCandidate
            SelectPreferredGateway(
                List<GatewayCandidate> candidates,
                WormholeInfo wormhole,
                Dictionary<Guid, int> usage,
                GatewayCandidate current,
                Guid? disallowedVessel)
        {
            if (wormhole == null || candidates == null)
                return null;

            GatewayCandidate refreshedCurrent = null;

            if (current != null &&
                current.Vessel != null &&
                current.Wormhole != null &&
                current.Wormhole.Body == wormhole.Body &&
                (!disallowedVessel.HasValue ||
                 current.Vessel.id != disallowedVessel.Value))
            {
                refreshedCurrent = candidates.FirstOrDefault(
                    candidate =>
                        candidate != null &&
                        candidate.Vessel != null &&
                        candidate.Vessel.id == current.Vessel.id &&
                        candidate.Wormhole != null &&
                        candidate.Wormhole.Body == wormhole.Body &&
                        candidate.IsValid &&
                        HasAvailableCapacity(candidate, usage));
            }

            GatewayCandidate strongest = SelectGateway(
                candidates,
                wormhole,
                usage,
                disallowedVessel);

            if (refreshedCurrent == null)
                return strongest;

            if (strongest == null || SameGateway(refreshedCurrent, strongest))
                return refreshedCurrent;

            // Do not reshuffle between nearly identical gateways. A new relay
            // must be at least five percentage points stronger before it takes
            // ownership of the wormhole endpoint.
            const double switchHysteresis = 0.05;

            if (strongest.RelaySignalStrength >=
                refreshedCurrent.RelaySignalStrength + switchHysteresis)
            {
                Debug.Log(
                    string.Format(
                        "[QuantumRelay] Stronger gateway selected | wormhole={0} | old={1} ({2:P0}) | new={3} ({4:P0})",
                        wormhole.Name,
                        refreshedCurrent.Vessel.vesselName,
                        refreshedCurrent.RelaySignalStrength,
                        strongest.Vessel.vesselName,
                        strongest.RelaySignalStrength));

                return strongest;
            }

            return refreshedCurrent;
        }

        private static GatewayCandidate SelectGateway(
            List<GatewayCandidate> candidates,
            WormholeInfo wormhole,
            Dictionary<Guid, int> usage,
            Guid? disallowedVessel)
        {
            if (wormhole == null ||
                candidates == null)
            {
                return null;
            }

            return candidates
                .Where(
                    candidate =>
                        candidate != null &&
                        candidate.IsValid &&
                        candidate.Wormhole != null &&
                        candidate.Wormhole.Body == wormhole.Body &&
                        candidate.Vessel != null &&
                        (!disallowedVessel.HasValue ||
                         candidate.Vessel.id != disallowedVessel.Value) &&
                        HasAvailableCapacity(candidate, usage))
                // Quantum synchronization strength is the primary selection
                // criterion. When a stronger official relay comes online, it
                // becomes the active gateway automatically.
                .OrderByDescending(
                    candidate => candidate.RelaySignalStrength)
                // Prefer higher-tier hardware when reported strengths match.
                .ThenByDescending(
                    candidate => candidate.RelayTier)
                // Preserve multi-link capacity as the next tie-breaker.
                .ThenByDescending(
                    candidate => GetRemainingCapacity(candidate, usage))
                // Prefer the healthier power reserve, then the closer vessel.
                .ThenByDescending(
                    candidate => GetElectricChargeFraction(candidate))
                .ThenBy(
                    candidate => candidate.DistanceMetres)
                .ThenBy(
                    candidate => candidate.Vessel.id)
                .FirstOrDefault();
        }

        private static double GetElectricChargeFraction(
            GatewayCandidate gateway)
        {
            if (gateway == null || gateway.ElectricChargeCapacity <= 0.0)
                return 0.0;

            return Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    gateway.ElectricChargeAmount /
                    gateway.ElectricChargeCapacity));
        }

        private static bool HasAvailableCapacity(
            GatewayCandidate gateway,
            Dictionary<Guid, int> usage)
        {
            return GetRemainingCapacity(
                       gateway,
                       usage) > 0;
        }

        private static int GetRemainingCapacity(
            GatewayCandidate gateway,
            Dictionary<Guid, int> usage)
        {
            if (gateway == null ||
                gateway.Vessel == null)
            {
                return 0;
            }

            int used = 0;

            if (usage != null)
                usage.TryGetValue(
                    gateway.Vessel.id,
                    out used);

            return Math.Max(
                0,
                GetGatewayCapacity(gateway) - used);
        }

        private static void ReserveGateway(
            GatewayCandidate gateway,
            Dictionary<Guid, int> usage)
        {
            if (gateway == null ||
                gateway.Vessel == null ||
                usage == null)
            {
                return;
            }

            int current;
            usage.TryGetValue(
                gateway.Vessel.id,
                out current);

            usage[gateway.Vessel.id] =
                current + 1;
        }

        private static int GetGatewayCapacity(
            GatewayCandidate gateway)
        {
            if (gateway == null)
                return 0;

            // Legacy reflector-only hardware remains limited to one bridge.
            if (!gateway.HasQuantumRelayModule)
                return 1;

            switch (gateway.RelayTier)
            {
                case 4:
                    return 6;

                case 3:
                    return 4;

                case 2:
                    return 2;

                default:
                    return 1;
            }
        }

        private static bool SameGateway(
            GatewayCandidate a,
            GatewayCandidate b)
        {
            if (a == null || b == null)
                return a == null && b == null;

            return
                a.Vessel != null &&
                b.Vessel != null &&
                a.Vessel.id == b.Vessel.id;
        }

        private List<QuantumBridge>
            GetEffectiveBridgeDefinitions()
        {
            // Explicit configuration and runtime discovery are merged. This is
            // important for mod packs with more than one wormhole pair: a
            // configured Kevbas bridge must not suppress discovery of Borgals.
            List<QuantumBridge> effective =
                new List<QuantumBridge>();

            HashSet<string> coveredPairs =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_bridgeDefinitions != null)
            {
                foreach (QuantumBridge bridge in _bridgeDefinitions)
                {
                    if (bridge == null || !bridge.Enabled)
                        continue;

                    if (string.IsNullOrEmpty(bridge.NetworkId))
                    {
                        bridge.NetworkId =
                            QuantumNetwork.CreateStableId(
                                bridge.GatewayA,
                                bridge.GatewayB);
                    }

                    effective.Add(bridge);
                    coveredPairs.Add(
                        QuantumNetwork.CreateStableId(
                            bridge.GatewayA,
                            bridge.GatewayB));
                }
            }

            HashSet<string> visitedEndpoints =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (WormholeInfo wormhole in _wormholes)
            {
                if (wormhole == null ||
                    string.IsNullOrEmpty(wormhole.Name) ||
                    visitedEndpoints.Contains(wormhole.Name))
                {
                    continue;
                }

                WormholeInfo partner =
                    _wormholes.FirstOrDefault(
                        candidate =>
                            candidate != null &&
                            candidate != wormhole &&
                            ((wormhole.Partner != null &&
                              candidate.Body == wormhole.Partner) ||
                             (candidate.Partner != null &&
                              wormhole.Body == candidate.Partner)));

                if (partner == null)
                    continue;

                visitedEndpoints.Add(wormhole.Name);
                visitedEndpoints.Add(partner.Name);

                string networkId =
                    QuantumNetwork.CreateStableId(
                        wormhole.Name,
                        partner.Name);

                if (coveredPairs.Contains(networkId))
                    continue;

                effective.Add(
                    new QuantumBridge
                    {
                        Name = "auto-" + networkId,
                        NetworkId = networkId,
                        DisplayName =
                            wormhole.Name +
                            " <-> " +
                            partner.Name,
                        GatewayA = wormhole.Name,
                        GatewayB = partner.Name,
                        Enabled = true
                    });

                coveredPairs.Add(networkId);
            }

            // Compatibility fallback for wormhole implementations that expose
            // tagged bodies but do not expose partner metadata. It is only used
            // when no configured or discovered network exists.
            if (effective.Count == 0 && _wormholes.Count >= 2)
            {
                string networkId =
                    QuantumNetwork.CreateStableId(
                        _wormholes[0].Name,
                        _wormholes[1].Name);

                effective.Add(
                    new QuantumBridge
                    {
                        Name = "legacy-primary",
                        NetworkId = networkId,
                        DisplayName = "Primary Quantum Link",
                        GatewayA = _wormholes[0].Name,
                        GatewayB = _wormholes[1].Name,
                        Enabled = true
                    });
            }

            return effective;
        }

        private void MaintainLinks()
        {
            double nowUt =
                Planetarium.GetUniversalTime();

            double elapsed =
                Math.Max(
                    0.0,
                    Math.Min(
                        nowUt -
                        _lastMaintenanceUt,
                        5.0));

            _lastMaintenanceUt = nowUt;

            for (int i = 0;
                 i < _links.Count;
                 i++)
            {
                ActiveQuantumLink link =
                    _links[i];

                if (link.GatewayA != null)
                {
                    link.GatewayA =
                        GatewayScanner.RefreshCandidate(
                            link.GatewayA);
                }

                if (link.GatewayB != null)
                {
                    link.GatewayB =
                        GatewayScanner.RefreshCandidate(
                            link.GatewayB);
                }

                if (!link.HasValidGateways)
                {
                    link.Online = false;
                    link.Reason =
                        "missing valid gateway";
                    MarkCacheDirty();
                    continue;
                }

                // Modern relay EC remains owned exclusively by
                // ModuleQuantumRelay's converter-backed power controller.
                // The network layer never applies a second hidden charge.
                bool aPowered =
                    link.GatewayA
                        .HasQuantumRelayModule ||
                    link.GatewayA
                        .HasElectricCharge;

                bool bPowered =
                    link.GatewayB
                        .HasQuantumRelayModule ||
                    link.GatewayB
                        .HasElectricCharge;

                link.Online =
                    aPowered &&
                    bPowered;

                link.Reason =
                    link.Online
                        ? "ready"
                        : "insufficient EC";
            }

            PublishLinks(
                _links.Count == 0
                    ? "no links configured"
                    : "network updated");
        }

        private void PublishLinks(string reason)
        {
            QuantumRelayRuntimeState.PublishLinks(
                _links,
                reason);

            QuantumGatewayManager.SetActiveLinks(
                _links);

            int onlineCount =
                _links.Count(
                    link =>
                        link != null &&
                        link.Online);

            if (onlineCount == _lastOnlineCount)
                return;

            _lastOnlineCount = onlineCount;

            string text =
                onlineCount > 0
                    ? "QUANTUM RELAY NETWORK ONLINE: " +
                      onlineCount +
                      " link(s)"
                    : "Quantum Relay network offline";

            QuantumRelayNotifications.Post(
                "network-links-" +
                onlineCount,
                text,
                true);

            QuantumRelayRuntimeState.SetTicker(
                text);
        }

        private WormholeInfo FindWormhole(
            string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return _wormholes.FirstOrDefault(
                wormhole =>
                    wormhole != null &&
                    string.Equals(
                        wormhole.Name,
                        name,
                        StringComparison
                            .OrdinalIgnoreCase));
        }

        private void LogCandidates(float realtime)
        {
            bool detailedLogDue =
                realtime >=
                _nextDetailedLogTime;

            if (detailedLogDue)
            {
                _nextDetailedLogTime =
                    realtime +
                    (float)QuantumRelaySettings
                        .DetailedLogIntervalSeconds;
            }

            foreach (
                GatewayCandidate candidate
                in _candidateCache)
            {
                string key =
                    candidate.Wormhole.Name +
                    ":" +
                    candidate.Vessel.id;

                string previous;

                bool changed =
                    !_lastStatus.TryGetValue(
                        key,
                        out previous) ||
                    previous !=
                        candidate.StatusKey;

                _lastStatus[key] =
                    candidate.StatusKey;

                if (changed ||
                    detailedLogDue)
                {
                    LogCandidate(candidate);
                }

                if (_hardwareDumped.Add(
                        candidate.Vessel.id))
                {
                    Debug.Log(
                        "[QuantumRelay] Hardware inventory | vessel=" +
                        candidate.Vessel.vesselName +
                        " | " +
                        GatewayScanner
                            .DescribeRelevantHardware(
                                candidate.Vessel));
                }
            }
        }

        private void RegisterEvents()
        {
            if (_quantumRelayEventsRegistered)
                return;

            _quantumRelayEventsRegistered = true;

            GameEvents.onVesselCreate.Add(
                OnVesselEvent);

            GameEvents.onVesselDestroy.Add(
                OnVesselEvent);

            GameEvents.onVesselWasModified.Add(
                OnVesselEvent);

            GameEvents.onVesselChange.Add(
                OnVesselEvent);
        }

        private void UnregisterEvents()
        {
            if (!_quantumRelayEventsRegistered)
                return;

            _quantumRelayEventsRegistered = false;

            GameEvents.onVesselCreate.Remove(
                OnVesselEvent);

            GameEvents.onVesselDestroy.Remove(
                OnVesselEvent);

            GameEvents.onVesselWasModified.Remove(
                OnVesselEvent);

            GameEvents.onVesselChange.Remove(
                OnVesselEvent);
        }

        private void OnVesselEvent(Vessel vessel)
        {
            MarkCacheDirty();
        }

        private void MarkCacheDirty()
        {
            _cacheDirty = true;

            float earliest =
                Time.realtimeSinceStartup +
                (float)QuantumRelaySettings
                    .DirtyScanDebounceSeconds;

            if (_dirtyAfterTime < earliest)
                _dirtyAfterTime = earliest;
        }

        private static void LogCandidate(
            GatewayCandidate candidate)
        {
            Debug.Log(
                string.Format(
                    "[QuantumRelay] Gateway scan | vessel={0} | " +
                    "near={1} | distance={2:N1} km | ready={3} | " +
                    "EC={4:N2}/{5:N2} | capacity={6} | VALID={7}",
                    candidate.Vessel.vesselName,
                    candidate.Wormhole.Name,
                    candidate.DistanceMetres /
                        1000.0,
                    candidate.RelayHardwareReady,
                    candidate.ElectricChargeAmount,
                    candidate.ElectricChargeCapacity,
                    GetGatewayCapacity(candidate),
                    candidate.IsValid));
        }
    }
}