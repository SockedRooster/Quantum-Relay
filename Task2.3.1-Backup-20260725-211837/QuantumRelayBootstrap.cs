using System;
using System.Collections.Generic;
using System.Linq;
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
        private GatewayCandidate _gatewayA;
        private GatewayCandidate _gatewayB;

        private float _nextFullScanTime;
        private float _nextMaintenanceTime;
        private float _nextDetailedLogTime;
        private float _dirtyAfterTime;
        private double _lastMaintenanceUt;
        private bool _cacheDirty = true;
        private bool _lastOnline;

        public void Start()
        {
            QuantumRelaySettings.Load();
            _lastMaintenanceUt = Planetarium.GetUniversalTime();
            RegisterEvents();
            CommNetNetworkInstaller.EnsureInstalled();
            Debug.Log("[QuantumRelay] Quantum Relay " + QuantumRelayConstants.DisplayVersion + " loaded | Developer: SockedRooster | Company: RoosterWorks | License: MIT");
            Debug.Log("[QuantumRelay] v1 creates the wormhole edge inside the stock CommNet graph rebuild; routing and signal handling remain stock.");
        }

        public void OnDestroy()
        {
            UnregisterEvents();
            QuantumGatewayManager.Clear();
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;

            // Stock CommNet can replace its graph after settings changes. Repair
            // that reset before updating gateway state.
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

            bool periodicScanDue = realtime >= _nextFullScanTime;
            bool dirtyScanDue = _cacheDirty && realtime >= _dirtyAfterTime;

            if (periodicScanDue || dirtyScanDue)
                RebuildGatewayCache(realtime);

            if (realtime < _nextMaintenanceTime) return;
            _nextMaintenanceTime = realtime + (float)QuantumRelaySettings.GatewayMaintenanceIntervalSeconds;
            MaintainSelectedGateways();
        }

        private void RebuildGatewayCache(float realtime)
        {
            _cacheDirty = false;
            _nextFullScanTime = realtime + (float)QuantumRelaySettings.FullGatewayScanIntervalSeconds;

            if (_wormholes == null || _wormholes.Count < 2)
                _wormholes = WormholeScanner.FindAll();
            if (_wormholes.Count < 2)
            {
                _candidateCache.Clear();
                _gatewayA = null;
                _gatewayB = null;
                SetOnline(null, null, false, "fewer than two wormholes detected");
                return;
            }

            _candidateCache = GatewayScanner.FindCandidates(_wormholes);
            SelectGateways();

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
                    Debug.Log("[QuantumRelay] Hardware inventory | vessel=" + candidate.Vessel.vesselName + " | " + GatewayScanner.DescribeRelevantHardware(candidate.Vessel));
            }
        }

        private void MaintainSelectedGateways()
        {
            double nowUt = Planetarium.GetUniversalTime();
            double elapsed = Math.Max(0.0, Math.Min(nowUt - _lastMaintenanceUt, 5.0));
            _lastMaintenanceUt = nowUt;

            if (_gatewayA != null)
                _gatewayA = GatewayScanner.RefreshCandidate(_gatewayA);
            if (_gatewayB != null)
                _gatewayB = GatewayScanner.RefreshCandidate(_gatewayB);

            if (_gatewayA == null || _gatewayB == null || !_gatewayA.IsValid || !_gatewayB.IsValid)
            {
                MarkCacheDirty();
                SetOnline(_gatewayA, _gatewayB, false, "missing valid gateway");
                return;
            }

            double needed = QuantumRelaySettings.ElectricChargePerSecondPerGateway * elapsed;
            bool aPowered = PowerManager.Consume(_gatewayA, needed);
            bool bPowered = PowerManager.Consume(_gatewayB, needed);
            bool powered = aPowered && bPowered;

            SetOnline(_gatewayA, _gatewayB, powered, powered ? "ready" : "insufficient EC");
        }

        private void SelectGateways()
        {
            WormholeInfo endA = _wormholes.FirstOrDefault(w => string.Equals(w.Name, QuantumRelaySettings.WormholeA, StringComparison.OrdinalIgnoreCase)) ?? _wormholes[0];
            WormholeInfo endB = _wormholes.FirstOrDefault(w => w.Body != endA.Body &&
                (endA.Partner == null || w.Body == endA.Partner)) ?? _wormholes.FirstOrDefault(w => w.Body != endA.Body);

            _gatewayA = SelectGateway(_candidateCache, endA, null);
            _gatewayB = SelectGateway(_candidateCache, endB, _gatewayA != null ? _gatewayA.Vessel : null);
        }

        private static GatewayCandidate SelectGateway(List<GatewayCandidate> candidates, WormholeInfo wormhole, Vessel excluded)
        {
            if (wormhole == null) return null;
            return candidates.Where(c => c.IsValid && c.Wormhole != null && c.Wormhole.Body == wormhole.Body && c.Vessel != excluded)
                .OrderByDescending(c => c.ElectricChargeAmount).FirstOrDefault();
        }

        private void RegisterEvents()
        {
            GameEvents.onVesselCreate.Add(OnVesselEvent);
            GameEvents.onVesselDestroy.Add(OnVesselEvent);
            GameEvents.onVesselWasModified.Add(OnVesselEvent);
            GameEvents.onVesselChange.Add(OnVesselEvent);
        }

        private void UnregisterEvents()
        {
            GameEvents.onVesselCreate.Remove(OnVesselEvent);
            GameEvents.onVesselDestroy.Remove(OnVesselEvent);
            GameEvents.onVesselWasModified.Remove(OnVesselEvent);
            GameEvents.onVesselChange.Remove(OnVesselEvent);
        }

        private void OnVesselEvent(Vessel vessel)
        {
            MarkCacheDirty();
        }

        private void MarkCacheDirty()
        {
            _cacheDirty = true;
            float earliest = Time.realtimeSinceStartup + (float)QuantumRelaySettings.DirtyScanDebounceSeconds;
            if (_dirtyAfterTime < earliest)
                _dirtyAfterTime = earliest;
        }

        private void SetOnline(GatewayCandidate a, GatewayCandidate b, bool online, string reason)
        {
            QuantumRelayRuntimeState.Publish(a, b, online, reason);
            QuantumGatewayManager.SetActive(a, b, online);
            if (online == _lastOnline) return;
            _lastOnline = online;
            string text = online ? "QUANTUM RELAY LINK ONLINE" : "Quantum Relay link offline: " + reason;
            QuantumRelayNotifications.Post(online ? "link-online" : "link-offline-" + reason, text, true);
        }

        private static void LogCandidate(GatewayCandidate c)
        {
            Debug.Log(string.Format(
                "[QuantumRelay] Gateway scan | vessel={0} | near={1} | distance={2:N1} km | loaded={3} | quantumModule={4} | relayHardware={5} | ready={6} | hardwareEvidence={7} | commNet={8} | commNetEvidence={9} | probe={10} | EC={11:N2}/{12:N2} | VALID={13}",
                c.Vessel.vesselName, c.Wormhole.Name, c.DistanceMetres / 1000.0, c.IsLoaded, c.HasQuantumRelayModule,
                c.HasRelayHardware, c.RelayHardwareReady, c.RelayHardwareEvidence ?? c.ReflectorEvidence, c.HasCommNet,
                c.CommNetEvidence, c.HasProbeControl, c.ElectricChargeAmount, c.ElectricChargeCapacity, c.IsValid));
        }
    }
}
