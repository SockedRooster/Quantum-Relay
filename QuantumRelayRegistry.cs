using System;
using System.IO;
using UnityEngine;

namespace QuantumRelay
{
    internal sealed class GatewayTelemetry
    {
        public Guid VesselId;
        public string VesselName = "Unknown";
        public string EndpointName = "Unknown";
        public string BodyName = "Unknown";
        public double DistanceMetres;
        public double ElectricChargeAmount;
        public double ElectricChargeCapacity;
        public bool Ready;
        public bool RelayHardwareReady;
        public bool HasCommNet;
        public bool HasProbeControl;
        public bool HasElectricCharge;
        public bool WasLoaded;

        public bool HasQuantumRelayModule;
        public bool QuantumRelayOperational;
        public string RelayOperationalState = "Unknown";
        public string RelayDeploymentState = "Unknown";
        public bool RelaySynchronized;
        public double RelaySynchronizationFraction;
        public double RelayPowerRate;
        public int RelayTier;
        public string RelayModel = "Unknown";
        public string RelayHardwareEvidence = "not found";

        public double UpdatedUt;

        public bool IsKnown
        {
            get
            {
                return VesselId != Guid.Empty ||
                       !string.IsNullOrEmpty(VesselName);
            }
        }
    }

    /// <summary>
    /// Save-specific mission-control telemetry. Flight publishes live gateway
    /// state; the Space Center and Tracking Station can display the last known
    /// state even when no vessel is currently loaded.
    /// </summary>
    internal static class QuantumRelayRegistry
    {
        private const string RootNodeName = "QUANTUM_RELAY_REGISTRY";

        private static bool _loaded;
        private static bool _online;
        private static string _reason = "No telemetry received";
        private static double _updatedUt;
        private static GatewayTelemetry _gatewayA;
        private static GatewayTelemetry _gatewayB;

        public static bool Online
        {
            get { EnsureLoaded(); return _online; }
        }

        public static string Reason
        {
            get { EnsureLoaded(); return _reason; }
        }

        public static double UpdatedUt
        {
            get { EnsureLoaded(); return _updatedUt; }
        }

        public static GatewayTelemetry GatewayA
        {
            get { EnsureLoaded(); return _gatewayA; }
        }

        public static GatewayTelemetry GatewayB
        {
            get { EnsureLoaded(); return _gatewayB; }
        }

        public static bool HasTelemetry
        {
            get
            {
                EnsureLoaded();
                return _gatewayA != null || _gatewayB != null;
            }
        }

        public static void Publish(
            GatewayCandidate a,
            GatewayCandidate b,
            bool online,
            string reason,
            bool save)
        {
            EnsureLoaded();

            double now = SafeUniversalTime();

            _gatewayA = FromCandidate(a, now) ?? _gatewayA;
            _gatewayB = FromCandidate(b, now) ?? _gatewayB;
            _online = online;
            _reason = string.IsNullOrEmpty(reason)
                ? "unknown"
                : reason;
            _updatedUt = now;

            if (save)
                Save();
        }

        public static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            Load();
        }

        public static void Reload()
        {
            _loaded = true;
            _gatewayA = null;
            _gatewayB = null;
            _online = false;
            _reason = "No telemetry received";
            _updatedUt = 0.0;
            Load();
        }

        public static string AgeText()
        {
            EnsureLoaded();

            if (_updatedUt <= 0.0)
                return "Never";

            double seconds = Math.Max(
                0.0,
                SafeUniversalTime() - _updatedUt);

            if (seconds < 60.0)
                return seconds.ToString("0") + " sec ago";

            if (seconds < 3600.0)
                return (seconds / 60.0).ToString("0.0") + " min ago";

            if (seconds < 86400.0)
                return (seconds / 3600.0).ToString("0.0") + " hr ago";

            return (seconds / 86400.0).ToString("0.0") + " days ago";
        }

        private static GatewayTelemetry FromCandidate(
            GatewayCandidate candidate,
            double now)
        {
            if (candidate == null || candidate.Vessel == null)
                return null;

            return new GatewayTelemetry
            {
                VesselId = candidate.Vessel.id,
                VesselName = candidate.Vessel.vesselName ?? "Unnamed",
                EndpointName = candidate.Wormhole != null
                    ? candidate.Wormhole.Name
                    : "Unknown",
                BodyName = candidate.Vessel.mainBody != null
                    ? candidate.Vessel.mainBody.bodyName
                    : "Unknown",
                DistanceMetres = candidate.DistanceMetres,
                ElectricChargeAmount = candidate.ElectricChargeAmount,
                ElectricChargeCapacity = candidate.ElectricChargeCapacity,
                Ready = candidate.IsValid,
                RelayHardwareReady = candidate.RelayHardwareReady,
                HasCommNet = candidate.HasCommNet,
                HasProbeControl = candidate.HasProbeControl,
                HasElectricCharge = candidate.HasElectricCharge,
                WasLoaded = candidate.IsLoaded,
                HasQuantumRelayModule =
                    candidate.HasQuantumRelayModule,
                QuantumRelayOperational =
                    candidate.QuantumRelayOperational,
                RelayOperationalState =
                    candidate.RelayOperationalState ?? "Unknown",
                RelayDeploymentState =
                    candidate.RelayDeploymentState ?? "Unknown",
                RelaySynchronized =
                    candidate.RelaySynchronized,
                RelaySynchronizationFraction =
                    candidate.RelaySynchronizationFraction,
                RelayPowerRate =
                    candidate.RelayPowerRate,
                RelayTier =
                    candidate.RelayTier,
                RelayModel =
                    candidate.RelayModel ?? "Unknown",
                RelayHardwareEvidence =
                    candidate.RelayHardwareEvidence ?? "not found",
                UpdatedUt = now
            };
        }

        private static void Load()
        {
            try
            {
                string path = RegistryPath();

                if (string.IsNullOrEmpty(path) ||
                    !File.Exists(path))
                {
                    return;
                }

                ConfigNode root = ConfigNode.Load(path);

                if (root == null ||
                    root.name != RootNodeName)
                {
                    return;
                }

                bool.TryParse(
                    root.GetValue("online"),
                    out _online);

                double.TryParse(
                    root.GetValue("updatedUt"),
                    out _updatedUt);

                _reason =
                    root.GetValue("reason") ?? _reason;

                _gatewayA =
                    ReadGateway(root.GetNode("GATEWAY_A"));

                _gatewayB =
                    ReadGateway(root.GetNode("GATEWAY_B"));

                Debug.Log(
                    "[QuantumRelay] Mission Control registry loaded | " +
                    "online=" + _online +
                    " | path=" + path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Unable to load Mission Control " +
                    "registry: " + exception.Message);
            }
        }

        private static void Save()
        {
            try
            {
                string path = RegistryPath();

                if (string.IsNullOrEmpty(path))
                    return;

                string directory =
                    Path.GetDirectoryName(path);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                ConfigNode root =
                    new ConfigNode(RootNodeName);

                root.AddValue(
                    "version",
                    QuantumRelayConstants.Version);
                root.AddValue("online", _online);
                root.AddValue(
                    "reason",
                    _reason ?? "unknown");
                root.AddValue("updatedUt", _updatedUt);

                AddGateway(
                    root,
                    "GATEWAY_A",
                    _gatewayA);

                AddGateway(
                    root,
                    "GATEWAY_B",
                    _gatewayB);

                root.Save(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Unable to save Mission Control " +
                    "registry: " + exception.Message);
            }
        }

        private static void AddGateway(
            ConfigNode root,
            string name,
            GatewayTelemetry gateway)
        {
            if (gateway == null)
                return;

            ConfigNode node = root.AddNode(name);

            node.AddValue(
                "vesselId",
                gateway.VesselId.ToString());
            node.AddValue(
                "vesselName",
                gateway.VesselName ?? "Unnamed");
            node.AddValue(
                "endpointName",
                gateway.EndpointName ?? "Unknown");
            node.AddValue(
                "bodyName",
                gateway.BodyName ?? "Unknown");
            node.AddValue(
                "distanceMetres",
                gateway.DistanceMetres);
            node.AddValue(
                "electricChargeAmount",
                gateway.ElectricChargeAmount);
            node.AddValue(
                "electricChargeCapacity",
                gateway.ElectricChargeCapacity);
            node.AddValue("ready", gateway.Ready);
            node.AddValue(
                "relayHardwareReady",
                gateway.RelayHardwareReady);
            node.AddValue(
                "hasCommNet",
                gateway.HasCommNet);
            node.AddValue(
                "hasProbeControl",
                gateway.HasProbeControl);
            node.AddValue(
                "hasElectricCharge",
                gateway.HasElectricCharge);
            node.AddValue(
                "wasLoaded",
                gateway.WasLoaded);

            node.AddValue(
                "hasQuantumRelayModule",
                gateway.HasQuantumRelayModule);
            node.AddValue(
                "quantumRelayOperational",
                gateway.QuantumRelayOperational);
            node.AddValue(
                "relayOperationalState",
                gateway.RelayOperationalState ?? "Unknown");
            node.AddValue(
                "relayDeploymentState",
                gateway.RelayDeploymentState ?? "Unknown");
            node.AddValue(
                "relaySynchronized",
                gateway.RelaySynchronized);
            node.AddValue(
                "relaySynchronizationFraction",
                gateway.RelaySynchronizationFraction);
            node.AddValue(
                "relayPowerRate",
                gateway.RelayPowerRate);
            node.AddValue(
                "relayTier",
                gateway.RelayTier);
            node.AddValue(
                "relayModel",
                gateway.RelayModel ?? "Unknown");
            node.AddValue(
                "relayHardwareEvidence",
                gateway.RelayHardwareEvidence ?? "not found");

            node.AddValue(
                "updatedUt",
                gateway.UpdatedUt);
        }

        private static GatewayTelemetry ReadGateway(
            ConfigNode node)
        {
            if (node == null)
                return null;

            Guid vesselId;
            Guid.TryParse(
                node.GetValue("vesselId"),
                out vesselId);

            return new GatewayTelemetry
            {
                VesselId = vesselId,
                VesselName =
                    node.GetValue("vesselName") ?? "Unnamed",
                EndpointName =
                    node.GetValue("endpointName") ?? "Unknown",
                BodyName =
                    node.GetValue("bodyName") ?? "Unknown",
                DistanceMetres =
                    ReadDouble(node, "distanceMetres"),
                ElectricChargeAmount =
                    ReadDouble(node, "electricChargeAmount"),
                ElectricChargeCapacity =
                    ReadDouble(node, "electricChargeCapacity"),
                Ready =
                    ReadBool(node, "ready"),
                RelayHardwareReady =
                    ReadBool(node, "relayHardwareReady"),
                HasCommNet =
                    ReadBool(node, "hasCommNet"),
                HasProbeControl =
                    ReadBool(node, "hasProbeControl"),
                HasElectricCharge =
                    ReadBool(node, "hasElectricCharge"),
                WasLoaded =
                    ReadBool(node, "wasLoaded"),

                HasQuantumRelayModule =
                    ReadBool(node, "hasQuantumRelayModule"),
                QuantumRelayOperational =
                    ReadBool(node, "quantumRelayOperational"),
                RelayOperationalState =
                    node.GetValue("relayOperationalState") ?? "Unknown",
                RelayDeploymentState =
                    node.GetValue("relayDeploymentState") ?? "Unknown",
                RelaySynchronized =
                    ReadBool(node, "relaySynchronized"),
                RelaySynchronizationFraction =
                    ReadDouble(node, "relaySynchronizationFraction"),
                RelayPowerRate =
                    ReadDouble(node, "relayPowerRate"),
                RelayTier =
                    ReadInt(node, "relayTier"),
                RelayModel =
                    node.GetValue("relayModel") ?? "Unknown",
                RelayHardwareEvidence =
                    node.GetValue("relayHardwareEvidence") ?? "not found",

                UpdatedUt =
                    ReadDouble(node, "updatedUt")
            };
        }

        private static bool ReadBool(
            ConfigNode node,
            string key)
        {
            bool value;

            return bool.TryParse(
                       node.GetValue(key),
                       out value) &&
                   value;
        }

        private static double ReadDouble(
            ConfigNode node,
            string key)
        {
            double value;

            return double.TryParse(
                node.GetValue(key),
                out value)
                ? value
                : 0.0;
        }

        private static int ReadInt(
            ConfigNode node,
            string key)
        {
            int value;

            return int.TryParse(
                node.GetValue(key),
                out value)
                ? value
                : 0;
        }

        private static string RegistryPath()
        {
            if (string.IsNullOrEmpty(HighLogic.SaveFolder))
                return null;

            return Path.Combine(
                KSPUtil.ApplicationRootPath,
                "saves",
                HighLogic.SaveFolder,
                "QuantumRelayRegistry.cfg");
        }

        private static double SafeUniversalTime()
        {
            try
            {
                return Planetarium.GetUniversalTime();
            }
            catch
            {
                return 0.0;
            }
        }
    }
}
