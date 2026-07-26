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
        private readonly Dictionary<string, string> _lastStatus = new Dictionary<string, string>();
        private readonly HashSet<Guid> _hardwareDumped = new HashSet<Guid>();

        private List<WormholeInfo> _wormholes = new List<WormholeInfo>();
        private List<GatewayCandidate> _candidateCache = new List<GatewayCandidate>();
        private List<QuantumBridge> _bridgeDefinitions = new List<QuantumBridge>();
        private readonly List<ActiveQuantumLink> _links = new List<ActiveQuantumLink>();

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
            Debug.Log("[QuantumRelay] Quantum Relay " + QuantumRelayConstants.DisplayVersion +
                      " loaded | multi-wormhole network foundation active");
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
            if (!HighLogic.LoadedSceneIsFlight) return;
            CommNetNetworkInstaller.EnsureInstalled();

            float realtime = Time.realtimeSinceStartup;
            if (QuantumRelayCommands.ConsumeRefresh())
            {
                _cacheDirty = true;
                _dirtyAfterTime = realtime;
                QuantumRelayRuntimeState.SetTicker("Gateway scan requested.");
            }

            if (QuantumRelayCommands.ConsumeRebuild())
            {
                QuantumGatewayManager.QueueRebuildNow();
                QuantumRelayRuntimeState.SetTicker("CommNet rebuild requested.");
            }

            if (realtime >= _nextFullScanTime || (_cacheDirty && realtime >= _dirtyAfterTime))
                RebuildGatewayCache(realtime);

            if (realtime < _nextMaintenanceTime) return;
            _nextMaintenanceTime = realtime + (float)QuantumRelaySettings.GatewayMaintenanceIntervalSeconds;
            MaintainLinks();
        }

        private void RebuildGatewayCache(float realtime)
        {
            _cacheDirty = false;
            _nextFullScanTime = realtime + (float)QuantumRelaySettings.FullGatewayScanIntervalSeconds;
            _wormholes = WormholeScanner.FindAll();

            if (_wormholes == null || _wormholes.Count < 2)
            {
                _candidateCache.Clear();
                _links.Clear();
                PublishLinks("fewer than two wormholes detected");
                return;
            }

            _candidateCache = GatewayScanner.FindCandidates(_wormholes);
            BuildLinks();
            LogCandidates(realtime);
        }

        private void BuildLinks()
        {
            _links.Clear();
            List<QuantumBridge> definitions = GetEffectiveBridgeDefinitions();
            HashSet<Guid> reservedGateways = new HashSet<Guid>();

            foreach (QuantumBridge definition in definitions)
            {
                if (definition == null || !definition.Enabled) continue;

                WormholeInfo endA = FindWormhole(definition.GatewayA);
                WormholeInfo endB = FindWormhole(definition.GatewayB);
                ActiveQuantumLink link = new ActiveQuantumLink
                {
                    Id = string.IsNullOrEmpty(definition.Name) ? Guid.NewGuid().ToString("N") : definition.Name,
                    DisplayName = string.IsNullOrEmpty(definition.DisplayName) ? definition.Name : definition.DisplayName,
                    GatewayA = SelectGateway(_candidateCache, endA, reservedGateways),
                    GatewayB = null,
                    Online = false,
                    Reason = "selecting gateways"
                };

                if (link.GatewayA != null)
                    reservedGateways.Add(link.GatewayA.Vessel.id);

                link.GatewayB = SelectGateway(_candidateCache, endB, reservedGateways);
                if (link.GatewayB != null)
                    reservedGateways.Add(link.GatewayB.Vessel.id);

                link.Reason = link.HasValidGateways ? "awaiting power" : "missing valid gateway";
                _links.Add(link);
            }

            PublishLinks(_links.Count == 0 ? "no bridge definitions" : "gateway selection complete");
        }

        private List<QuantumBridge> GetEffectiveBridgeDefinitions()
        {
            if (_bridgeDefinitions != null && _bridgeDefinitions.Count > 0)
                return _bridgeDefinitions.Where(b => b != null && b.Enabled).ToList();

            List<QuantumBridge> generated = new List<QuantumBridge>();
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (WormholeInfo wormhole in _wormholes)
            {
                if (wormhole == null || string.IsNullOrEmpty(wormhole.Name) || used.Contains(wormhole.Name))
                    continue;

                WormholeInfo partner = _wormholes.FirstOrDefault(w =>
                    w != null && w != wormhole && !used.Contains(w.Name) &&
                    ((wormhole.Partner != null && w.Body == wormhole.Partner) ||
                     (w.Partner != null && wormhole.Body == w.Partner)));

                if (partner == null) continue;
                used.Add(wormhole.Name);
                used.Add(partner.Name);
                generated.Add(new QuantumBridge
                {
                    Name = "auto-" + generated.Count,
                    DisplayName = wormhole.Name + " <-> " + partner.Name,
                    GatewayA = wormhole.Name,
                    GatewayB = partner.Name,
                    Enabled = true
                });
            }

            if (generated.Count == 0 && _wormholes.Count >= 2)
            {
                generated.Add(new QuantumBridge
                {
                    Name = "legacy-primary",
                    DisplayName = "Primary Quantum Link",
                    GatewayA = _wormholes[0].Name,
                    GatewayB = _wormholes[1].Name,
                    Enabled = true
                });
            }
            return generated;
        }

        private void MaintainLinks()
        {
            double nowUt = Planetarium.GetUniversalTime();
            double elapsed = Math.Max(0.0, Math.Min(nowUt - _lastMaintenanceUt, 5.0));
            _lastMaintenanceUt = nowUt;

            for (int i = 0; i < _links.Count; i++)
            {
                ActiveQuantumLink link = _links[i];
                if (link.GatewayA != null) link.GatewayA = GatewayScanner.RefreshCandidate(link.GatewayA);
                if (link.GatewayB != null) link.GatewayB = GatewayScanner.RefreshCandidate(link.GatewayB);

                if (!link.HasValidGateways)
                {
                    link.Online = false;
                    link.Reason = "missing valid gateway";
                    MarkCacheDirty();
                    continue;
                }

                double needed = QuantumRelaySettings.ElectricChargePerSecondPerGateway * elapsed;
                bool aPowered = PowerManager.Consume(link.GatewayA, needed);
                bool bPowered = PowerManager.Consume(link.GatewayB, needed);
                link.Online = aPowered && bPowered;
                link.Reason = link.Online ? "ready" : "insufficient EC";
            }

            PublishLinks(_links.Count == 0 ? "no links configured" : "network updated");
        }

        private void PublishLinks(string reason)
        {
            QuantumRelayRuntimeState.PublishLinks(_links, reason);
            QuantumGatewayManager.SetActiveLinks(_links);

            int onlineCount = _links.Count(l => l != null && l.Online);
            if (onlineCount == _lastOnlineCount) return;
            _lastOnlineCount = onlineCount;

            string text = onlineCount > 0
                ? "QUANTUM RELAY NETWORK ONLINE: " + onlineCount + " link(s)"
                : "Quantum Relay network offline";
            QuantumRelayNotifications.Post("network-links-" + onlineCount, text, true);
            QuantumRelayRuntimeState.SetTicker(text);
        }

        private WormholeInfo FindWormhole(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return _wormholes.FirstOrDefault(w => w != null &&
                string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static GatewayCandidate SelectGateway(
            List<GatewayCandidate> candidates,
            WormholeInfo wormhole,
            HashSet<Guid> excluded)
        {
            if (wormhole == null) return null;
            return candidates
                .Where(c => c != null && c.IsValid && c.Wormhole != null &&
                            c.Wormhole.Body == wormhole.Body && c.Vessel != null &&
                            (excluded == null || !excluded.Contains(c.Vessel.id)))
                .OrderByDescending(c => c.ElectricChargeAmount)
                .FirstOrDefault();
        }

        private void LogCandidates(float realtime)
        {
            bool detailedLogDue = realtime >= _nextDetailedLogTime;
            if (detailedLogDue)
                _nextDetailedLogTime = realtime + (float)QuantumRelaySettings.DetailedLogIntervalSeconds;

            foreach (GatewayCandidate candidate in _candidateCache)
            {
                string key = candidate.Wormhole.Name + ":" + candidate.Vessel.id;
                string previous;
                bool changed = !_lastStatus.TryGetValue(key, out previous) || previous != candidate.StatusKey;
                _lastStatus[key] = candidate.StatusKey;
                if (changed || detailedLogDue) LogCandidate(candidate);
                if (_hardwareDumped.Add(candidate.Vessel.id))
                    Debug.Log("[QuantumRelay] Hardware inventory | vessel=" + candidate.Vessel.vesselName +
                              " | " + GatewayScanner.DescribeRelevantHardware(candidate.Vessel));
            }
        }

        private void RegisterEvents()
        {
            if (_quantumRelayEventsRegistered) return;
            _quantumRelayEventsRegistered = true;
            GameEvents.onVesselCreate.Add(OnVesselEvent);
            GameEvents.onVesselDestroy.Add(OnVesselEvent);
            GameEvents.onVesselWasModified.Add(OnVesselEvent);
            GameEvents.onVesselChange.Add(OnVesselEvent);
        }

        private void UnregisterEvents()
        {
            if (!_quantumRelayEventsRegistered) return;
            _quantumRelayEventsRegistered = false;
            GameEvents.onVesselCreate.Remove(OnVesselEvent);
            GameEvents.onVesselDestroy.Remove(OnVesselEvent);
            GameEvents.onVesselWasModified.Remove(OnVesselEvent);
            GameEvents.onVesselChange.Remove(OnVesselEvent);
        }

        private void OnVesselEvent(Vessel vessel) { MarkCacheDirty(); }

        private void MarkCacheDirty()
        {
            _cacheDirty = true;
            float earliest = Time.realtimeSinceStartup + (float)QuantumRelaySettings.DirtyScanDebounceSeconds;
            if (_dirtyAfterTime < earliest) _dirtyAfterTime = earliest;
        }

        private static void LogCandidate(GatewayCandidate c)
        {
            Debug.Log(string.Format(
                "[QuantumRelay] Gateway scan | vessel={0} | near={1} | distance={2:N1} km | ready={3} | EC={4:N2}/{5:N2} | VALID={6}",
                c.Vessel.vesselName, c.Wormhole.Name, c.DistanceMetres / 1000.0,
                c.RelayHardwareReady, c.ElectricChargeAmount, c.ElectricChargeCapacity, c.IsValid));
        }
    }
}
