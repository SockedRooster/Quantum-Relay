using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Supplies live proto-vessel telemetry while at the Space Center or
    /// Tracking Station. If KSP has not populated the vessel list yet, the GUI
    /// continues to use the save-specific registry's last known telemetry.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    internal sealed class QuantumRelayMissionControl : MonoBehaviour
    {
        private float _nextScan;

        public void Start()
        {
            if (!IsMissionControlScene())
                return;

            QuantumRelaySettings.Load();
            QuantumRelayRegistry.Reload();
            _nextScan =
                Time.realtimeSinceStartup + 0.75f;
        }

        public void Update()
        {
            if (!IsMissionControlScene() ||
                Time.realtimeSinceStartup < _nextScan)
            {
                return;
            }

            _nextScan =
                Time.realtimeSinceStartup + 3.0f;

            ScanPersistentVessels();
        }

        private static void ScanPersistentVessels()
        {
            try
            {
                if (FlightGlobals.Vessels == null ||
                    FlightGlobals.Vessels.Count == 0)
                {
                    return;
                }

                List<WormholeInfo> wormholes =
                    WormholeScanner.FindAll();

                if (wormholes == null ||
                    wormholes.Count < 2)
                {
                    return;
                }

                List<GatewayCandidate> candidates =
                    GatewayScanner.FindCandidates(wormholes);

                WormholeInfo endA =
                    wormholes.FirstOrDefault(
                        wormhole =>
                            string.Equals(
                                wormhole.Name,
                                QuantumRelaySettings.WormholeA,
                                StringComparison.OrdinalIgnoreCase))
                    ?? wormholes[0];

                WormholeInfo endB =
                    wormholes.FirstOrDefault(
                        wormhole =>
                            wormhole.Body != endA.Body &&
                            (endA.Partner == null ||
                             wormhole.Body == endA.Partner))
                    ?? wormholes.FirstOrDefault(
                        wormhole =>
                            wormhole.Body != endA.Body);

                GatewayCandidate gatewayA =
                    SelectGateway(
                        candidates,
                        endA,
                        null);

                GatewayCandidate gatewayB =
                    SelectGateway(
                        candidates,
                        endB,
                        gatewayA != null
                            ? gatewayA.Vessel
                            : null);

                if (gatewayA == null &&
                    gatewayB == null)
                {
                    return;
                }

                bool online =
                    gatewayA != null &&
                    gatewayB != null &&
                    gatewayA.IsValid &&
                    gatewayB.IsValid;

                string reason =
                    online
                        ? "mission control telemetry"
                        : BuildOfflineReason(
                            gatewayA,
                            gatewayB);

                QuantumRelayRuntimeState.Publish(
                    gatewayA,
                    gatewayB,
                    online,
                    reason);

                QuantumRelayRuntimeState.SetTicker(
                    online
                        ? "Mission Control: quantum bridge online."
                        : "Mission Control: " + reason + ".");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Mission Control telemetry scan " +
                    "failed: " + exception.Message);
            }
        }

        private static GatewayCandidate SelectGateway(
            List<GatewayCandidate> candidates,
            WormholeInfo wormhole,
            Vessel excluded)
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
                        candidate.Wormhole != null &&
                        candidate.Wormhole.Body ==
                            wormhole.Body &&
                        candidate.Vessel != excluded)
                .OrderByDescending(
                    candidate => candidate.IsValid)
                .ThenByDescending(
                    candidate =>
                        candidate.QuantumRelayOperational)
                .ThenByDescending(
                    candidate => candidate.RelayTier)
                .ThenByDescending(
                    candidate =>
                        candidate.RelaySynchronizationFraction)
                .ThenByDescending(
                    candidate =>
                        candidate.ElectricChargeAmount)
                .FirstOrDefault();
        }

        private static string BuildOfflineReason(
            GatewayCandidate gatewayA,
            GatewayCandidate gatewayB)
        {
            if (gatewayA == null &&
                gatewayB == null)
            {
                return "no gateway telemetry";
            }

            if (gatewayA == null)
                return "Gateway A not found";

            if (gatewayB == null)
                return "Gateway B not found";

            if (!gatewayA.IsValid)
            {
                return DescribeWaitingGateway(
                    "Gateway A",
                    gatewayA);
            }

            if (!gatewayB.IsValid)
            {
                return DescribeWaitingGateway(
                    "Gateway B",
                    gatewayB);
            }

            return "gateway telemetry incomplete";
        }

        private static string DescribeWaitingGateway(
            string label,
            GatewayCandidate gateway)
        {
            if (gateway.HasQuantumRelayModule)
            {
                return label + " " +
                       FormatState(
                           gateway.RelayOperationalState);
            }

            return label +
                   " legacy hardware not ready";
        }

        private static string FormatState(string state)
        {
            if (string.IsNullOrEmpty(state))
                return "not operational";

            switch (state)
            {
                case "InsufficientPower":
                    return "has insufficient power";
                case "NoCommNetHardware":
                    return "has no CommNet hardware";
                case "HardwareFault":
                    return "has a hardware fault";
                default:
                    return "is " + state;
            }
        }

        private static bool IsMissionControlScene()
        {
            return HighLogic.LoadedScene ==
                       GameScenes.SPACECENTER ||
                   HighLogic.LoadedScene ==
                       GameScenes.TRACKSTATION;
        }
    }
}
