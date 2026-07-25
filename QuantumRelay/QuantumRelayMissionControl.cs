using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Supplies live proto-vessel telemetry while at the Space Center or Tracking
    /// Station. If KSP has not populated the vessel list yet, the GUI continues to
    /// use the save-specific registry's last known telemetry instead of reporting DOWN.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    internal sealed class QuantumRelayMissionControl : MonoBehaviour
    {
        private float _nextScan;

        public void Start()
        {
            if (!IsMissionControlScene()) return;
            QuantumRelaySettings.Load();
            QuantumRelayRegistry.Reload();
            _nextScan = Time.realtimeSinceStartup + 0.75f;
        }

        public void Update()
        {
            if (!IsMissionControlScene() || Time.realtimeSinceStartup < _nextScan) return;
            _nextScan = Time.realtimeSinceStartup + 3.0f;
            ScanPersistentVessels();
        }

        private static void ScanPersistentVessels()
        {
            try
            {
                if (FlightGlobals.Vessels == null || FlightGlobals.Vessels.Count == 0) return;
                List<WormholeInfo> wormholes = WormholeScanner.FindAll();
                if (wormholes == null || wormholes.Count < 2) return;

                List<GatewayCandidate> candidates = GatewayScanner.FindCandidates(wormholes);
                WormholeInfo endA = wormholes.FirstOrDefault(w => string.Equals(w.Name, QuantumRelaySettings.WormholeA, StringComparison.OrdinalIgnoreCase)) ?? wormholes[0];
                WormholeInfo endB = wormholes.FirstOrDefault(w => w.Body != endA.Body && (endA.Partner == null || w.Body == endA.Partner))
                    ?? wormholes.FirstOrDefault(w => w.Body != endA.Body);

                GatewayCandidate a = SelectGateway(candidates, endA, null);
                GatewayCandidate b = SelectGateway(candidates, endB, a != null ? a.Vessel : null);
                if (a == null && b == null) return;

                bool online = a != null && b != null && a.IsValid && b.IsValid;
                QuantumRelayRuntimeState.Publish(a, b, online, online ? "mission control telemetry" : "gateway telemetry incomplete");
                QuantumRelayRuntimeState.SetTicker(online ? "Mission Control telemetry: bridge online." : "Mission Control telemetry updated.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QuantumRelay] Mission Control telemetry scan failed: " + ex.Message);
            }
        }

        private static GatewayCandidate SelectGateway(List<GatewayCandidate> candidates, WormholeInfo wormhole, Vessel excluded)
        {
            if (wormhole == null || candidates == null) return null;
            return candidates.Where(c => c != null && c.Wormhole != null && c.Wormhole.Body == wormhole.Body && c.Vessel != excluded)
                .OrderByDescending(c => c.IsValid)
                .ThenByDescending(c => c.ElectricChargeAmount).FirstOrDefault();
        }

        private static bool IsMissionControlScene()
        {
            return HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.TRACKSTATION;
        }
    }
}
