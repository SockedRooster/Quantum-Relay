using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace QuantumRelay
{
    internal sealed class GatewayCandidate
    {
        public Vessel Vessel { get; set; }
        public WormholeInfo Wormhole { get; set; }
        public double DistanceMetres { get; set; }
        public bool IsLoaded { get; set; }

        // Legacy reflector diagnostics remain available for compatibility.
        public bool HasReflector { get; set; }
        public bool ReflectorDeployed { get; set; }
        public string ReflectorEvidence { get; set; }

        // Quantum Relay hardware state.
        public bool HasQuantumRelayModule { get; set; }
        public bool QuantumRelayOperational { get; set; }
        public string RelayOperationalState { get; set; }
        public string RelayDeploymentState { get; set; }
        public bool RelaySynchronized { get; set; }
        public double RelaySynchronizationFraction { get; set; }
        public double RelayPowerRate { get; set; }
        public int RelayTier { get; set; }
        public string RelayModel { get; set; }
        public double RelaySignalStrength { get; set; }
        public string RelayHardwareEvidence { get; set; }

        // Vessel diagnostics retained for legacy hardware and UI reporting.
        public bool HasCommNet { get; set; }
        public string CommNetEvidence { get; set; }
        public bool HasProbeControl { get; set; }
        public double ElectricChargeAmount { get; set; }
        public double ElectricChargeCapacity { get; set; }

        public bool HasElectricCharge
        {
            get { return ElectricChargeAmount > 0.01; }
        }

        public bool HasRelayHardware
        {
            get { return HasQuantumRelayModule; }
        }

        public bool RelayHardwareReady
        {
            get { return HasQuantumRelayModule && QuantumRelayOperational; }
        }

        public bool IsValid
        {
            get
            {
                return Vessel != null &&
                       Wormhole != null &&
                       HasQuantumRelayModule &&
                       QuantumRelayOperational &&
                       HasProbeControl;
            }
        }

        public string StatusKey
        {
            get
            {
                return string.Join(
                    "|",
                    IsValid,
                    HasQuantumRelayModule,
                    QuantumRelayOperational,
                    RelayOperationalState ?? string.Empty,
                    RelayDeploymentState ?? string.Empty,
                    RelaySynchronized,
                    RelaySynchronizationFraction,
                    RelayPowerRate,
                    RelayTier,
                    RelayModel ?? string.Empty,
                    RelaySignalStrength,
                    HasReflector,
                    ReflectorDeployed,
                    HasCommNet,
                    HasProbeControl,
                    HasElectricCharge,
                    RelayHardwareEvidence ?? string.Empty,
                    ReflectorEvidence ?? string.Empty,
                    CommNetEvidence ?? string.Empty);
            }
        }
    }

    internal static class GatewayScanner
    {
        public static List<GatewayCandidate> FindCandidates(
            IEnumerable<WormholeInfo> wormholes)
        {
            var results = new List<GatewayCandidate>();

            if (FlightGlobals.Vessels == null || wormholes == null)
                return results;

            foreach (WormholeInfo wormhole in wormholes)
            {
                if (wormhole == null || wormhole.Body == null)
                    continue;

                foreach (Vessel vessel in
                    FlightGlobals.Vessels.Where(IsScannableVessel))
                {
                    double distance;

                    try
                    {
                        distance = Vector3d.Distance(
                            vessel.GetWorldPos3D(),
                            wormhole.Body.position);
                    }
                    catch
                    {
                        continue;
                    }

                    if (distance >
                        QuantumRelaySettings.GatewayRadiusMetres)
                    {
                        continue;
                    }

                    results.Add(
                        vessel.loaded
                            ? InspectLoaded(vessel, wormhole, distance)
                            : InspectUnloaded(vessel, wormhole, distance));
                }
            }

            return results;
        }

        public static GatewayCandidate RefreshCandidate(
            GatewayCandidate previous)
        {
            if (previous == null ||
                previous.Vessel == null ||
                previous.Wormhole == null ||
                previous.Wormhole.Body == null)
            {
                return null;
            }

            Vessel vessel = previous.Vessel;

            if (!IsScannableVessel(vessel))
                return null;

            double distance;

            try
            {
                distance = Vector3d.Distance(
                    vessel.GetWorldPos3D(),
                    previous.Wormhole.Body.position);
            }
            catch
            {
                return null;
            }

            if (distance >
                QuantumRelaySettings.GatewayRadiusMetres)
            {
                return NewCandidate(
                    vessel,
                    previous.Wormhole,
                    distance);
            }

            return vessel.loaded
                ? InspectLoaded(
                    vessel,
                    previous.Wormhole,
                    distance)
                : InspectUnloaded(
                    vessel,
                    previous.Wormhole,
                    distance);
        }

        private static GatewayCandidate NewCandidate(
            Vessel vessel,
            WormholeInfo wormhole,
            double distance)
        {
            return new GatewayCandidate
            {
                Vessel = vessel,
                Wormhole = wormhole,
                DistanceMetres = distance,
                IsLoaded = vessel != null && vessel.loaded,
                ReflectorEvidence = "not found",
                RelayHardwareEvidence = "not found",
                RelayOperationalState = "Not Installed",
                RelayDeploymentState = "Unknown",
                RelayModel = "Unknown",
                CommNetEvidence = "not found"
            };
        }

        private static GatewayCandidate InspectLoaded(
            Vessel vessel,
            WormholeInfo wormhole,
            double distance)
        {
            GatewayCandidate result =
                NewCandidate(vessel, wormhole, distance);

            if (vessel.parts != null)
            {
                foreach (Part part in vessel.parts)
                {
                    InspectLoadedRelayModule(part, result);

                    if (FindModule(
                            part,
                            QuantumRelaySettings.CommandModuleName) != null)
                    {
                        result.HasProbeControl = true;
                    }
                }
            }

            result.HasCommNet =
                TryGetCommNetCapability(vessel, out string evidence);
            result.CommNetEvidence = evidence;

            try
            {
                vessel.GetConnectedResourceTotals(
                    PartResourceLibrary.ElectricityHashcode,
                    out double amount,
                    out double capacity);

                result.ElectricChargeAmount = amount;
                result.ElectricChargeCapacity = capacity;
            }
            catch
            {
                result.ElectricChargeAmount = 0.0;
                result.ElectricChargeCapacity = 0.0;
            }

            return result;
        }

        private static void InspectLoadedRelayModule(
            Part part,
            GatewayCandidate result)
        {
            PartModule relayPartModule =
                FindModule(
                    part,
                    QuantumRelayConstants.QuantumRelayModuleName);

            if (relayPartModule == null)
                return;

            result.HasQuantumRelayModule = true;

            ModuleQuantumRelay relay =
                relayPartModule as ModuleQuantumRelay;

            if (relay == null)
            {
                result.RelayHardwareEvidence =
                    "ModuleQuantumRelay runtime type unavailable";
                result.ReflectorEvidence =
                    result.RelayHardwareEvidence;
                return;
            }

            bool operational = relay.IsOperational();

            // A vessel may eventually carry more than one relay module.
            // Any fully operational module makes the vessel relay-ready.
            result.QuantumRelayOperational =
                result.QuantumRelayOperational || operational;

            result.ReflectorDeployed =
                result.ReflectorDeployed ||
                relay.DeploymentState ==
                    QuantumRelayDeploymentState.Fixed ||
                relay.DeploymentState ==
                    QuantumRelayDeploymentState.Extended;

            if (operational ||
                string.IsNullOrEmpty(result.RelayOperationalState) ||
                result.RelayOperationalState == "Not Installed")
            {
                result.RelayOperationalState =
                    relay.OperationalStateName;
                result.RelayDeploymentState =
                    relay.DeploymentState.ToString();
                result.RelaySynchronized =
                    relay.IsSynchronized;
                result.RelaySynchronizationFraction =
                    relay.SynchronizationFraction;
                result.RelayPowerRate =
                    relay.CurrentPowerRate;
                result.RelayTier =
                    relay.RelayTier;
                result.RelayModel =
                    relay.relayModel;
                result.RelaySignalStrength =
                    relay.SignalStrengthMultiplier;
            }

            result.RelayHardwareEvidence = string.Format(
                "ModuleQuantumRelay; model={0}; tier={1}; " +
                "enabled={2}; deployment={3}; state={4}; " +
                "synchronized={5}; synchronization={6:P0}; " +
                "powerRate={7:N2} EC/s; signal={8:P0}",
                relay.relayModel,
                relay.RelayTier,
                relay.relayEnabled,
                relay.DeploymentState,
                relay.OperationalStateName,
                relay.IsSynchronized,
                relay.SynchronizationFraction,
                relay.CurrentPowerRate,
                relay.SignalStrengthMultiplier);

            result.ReflectorEvidence =
                result.RelayHardwareEvidence;
        }

        private static GatewayCandidate InspectUnloaded(
            Vessel vessel,
            WormholeInfo wormhole,
            double distance)
        {
            GatewayCandidate result =
                NewCandidate(vessel, wormhole, distance);

            ProtoVessel proto = vessel.protoVessel;

            if (proto != null &&
                proto.protoPartSnapshots != null)
            {
                foreach (ProtoPartSnapshot part in
                    proto.protoPartSnapshots)
                {
                    InspectUnloadedRelayModule(part, result);

                    if (FindProtoModule(
                            part,
                            QuantumRelaySettings.CommandModuleName) != null)
                    {
                        result.HasProbeControl = true;
                    }

                    ReadProtoElectricCharge(part, result);
                }
            }

            result.HasCommNet =
                TryGetCommNetCapability(vessel, out string evidence);
            result.CommNetEvidence = evidence;

            return result;
        }

        private static void InspectUnloadedRelayModule(
            ProtoPartSnapshot part,
            GatewayCandidate result)
        {
            ProtoPartModuleSnapshot relay =
                FindProtoModule(
                    part,
                    QuantumRelayConstants.QuantumRelayModuleName);

            if (relay == null)
                return;

            // Relay hardware fields configured in a part CFG are not normally
            // persisted into ProtoPartModuleSnapshot.moduleValues. Use the
            // unloaded part's prefab module as the authoritative fallback.
            ModuleQuantumRelay prefabRelay =
                FindPrefabRelayModule(part);

            result.HasQuantumRelayModule = true;

            bool enabled =
                ReadProtoBool(
                    relay,
                    "relayEnabled",
                    prefabRelay != null
                        ? prefabRelay.relayEnabled
                        : true);
            bool requiresDeployment =
                ReadProtoBool(
                    relay,
                    "requiresDeployment",
                    prefabRelay != null
                        ? prefabRelay.requiresDeployment
                        : true);
            bool synchronized =
                ReadProtoBool(relay, "relaySynchronized", false);

            double synchronizationFraction =
                ReadProtoDouble(
                    relay,
                    "synchronizationProgress",
                    synchronized ? 1.0 : 0.0);

            string persistedState =
                ReadProtoString(
                    relay,
                    "persistedOperationalState",
                    null);

            string configuredClass =
                ReadProtoString(
                    relay,
                    "relayClass",
                    prefabRelay != null
                        ? prefabRelay.relayClass
                        : RelayClass.Pioneer.ToString());

            RelayClass relayClass =
                RelayCatalog.Parse(configuredClass, 1);
            RelayDefinition definition =
                RelayCatalog.Get(relayClass);
            string model = definition.DisplayName;
            int tier = definition.Tier;

            double idlePowerRate =
                ReadProtoDouble(
                    relay,
                    "idlePowerRate",
                    prefabRelay != null
                        ? prefabRelay.idlePowerRate
                        : 0.02);

            double synchronizationPowerRate =
                ReadProtoDouble(
                    relay,
                    "synchronizationPowerRate",
                    prefabRelay != null
                        ? prefabRelay.synchronizationPowerRate
                        : 1.0);

            double operationalPowerRate =
                ReadProtoDouble(
                    relay,
                    "operationalPowerRate",
                    prefabRelay != null
                        ? prefabRelay.operationalPowerRate
                        : 0.5);

            double signalStrength =
                definition.SynchronizationStrength;

            string deploymentModuleName =
                ReadProtoString(
                    relay,
                    "deploymentModuleName",
                    prefabRelay != null
                        ? prefabRelay.deploymentModuleName
                        : QuantumRelaySettings.ReflectorModuleName);

            QuantumRelayDeploymentState deploymentState =
                GetUnloadedDeploymentState(
                    part,
                    requiresDeployment,
                    deploymentModuleName);

            bool operational =
                IsPersistedOperationalState(persistedState);

            // Compatibility for vessels saved before the Phase A state fields
            // existed. This path disappears naturally after the vessel loads
            // once and ModuleQuantumRelay persists its full state.
            if (string.IsNullOrEmpty(persistedState) ||
                string.Equals(
                    persistedState,
                    "Unknown",
                    StringComparison.OrdinalIgnoreCase))
            {
                operational =
                    enabled &&
                    (deploymentState ==
                         QuantumRelayDeploymentState.Fixed ||
                     deploymentState ==
                         QuantumRelayDeploymentState.Extended);
            }

            result.QuantumRelayOperational =
                result.QuantumRelayOperational || operational;

            result.ReflectorDeployed =
                result.ReflectorDeployed ||
                deploymentState ==
                    QuantumRelayDeploymentState.Fixed ||
                deploymentState ==
                    QuantumRelayDeploymentState.Extended;

            if (operational ||
                string.IsNullOrEmpty(result.RelayOperationalState) ||
                result.RelayOperationalState == "Not Installed")
            {
                result.RelayOperationalState =
                    string.IsNullOrEmpty(persistedState)
                        ? (operational
                            ? "Operational (Legacy Save)"
                            : "Unknown")
                        : persistedState;

                result.RelayDeploymentState =
                    deploymentState.ToString();
                result.RelaySynchronized =
                    synchronized;
                result.RelaySynchronizationFraction =
                    synchronizationFraction;

                double displayedPowerRate = 0.0;
                if (enabled)
                {
                    if (operational)
                    {
                        displayedPowerRate = operationalPowerRate;
                    }
                    else if (!synchronized &&
                             (deploymentState ==
                                  QuantumRelayDeploymentState.Fixed ||
                              deploymentState ==
                                  QuantumRelayDeploymentState.Extended))
                    {
                        displayedPowerRate = synchronizationPowerRate;
                    }
                    else
                    {
                        displayedPowerRate = idlePowerRate;
                    }
                }

                result.RelayPowerRate = Math.Max(0.0, displayedPowerRate);
                result.RelayTier =
                    tier;
                result.RelayModel =
                    model;
                result.RelaySignalStrength =
                    Math.Max(0.0, Math.Min(1.25, signalStrength));
            }

            result.RelayHardwareEvidence = string.Format(
                "ModuleQuantumRelay proto; model={0}; class={1}; " +
                "enabled={2}; deployment={3}; persistedState={4}; " +
                "synchronized={5}; synchronization={6:P0}",
                model,
                relayClass,
                enabled,
                deploymentState,
                string.IsNullOrEmpty(persistedState)
                    ? "missing"
                    : persistedState,
                synchronized,
                synchronizationFraction);

            result.ReflectorEvidence =
                result.RelayHardwareEvidence;
        }

        private static ModuleQuantumRelay FindPrefabRelayModule(
            ProtoPartSnapshot part)
        {
            if (part == null || string.IsNullOrEmpty(part.partName))
                return null;

            try
            {
                AvailablePart availablePart =
                    PartLoader.getPartInfoByName(part.partName);

                if (availablePart == null || availablePart.partPrefab == null)
                    return null;

                return availablePart.partPrefab
                    .FindModuleImplementing<ModuleQuantumRelay>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static QuantumRelayDeploymentState
            GetUnloadedDeploymentState(
                ProtoPartSnapshot part,
                bool requiresDeployment,
                string deploymentModuleName)
        {
            if (!requiresDeployment)
                return QuantumRelayDeploymentState.Fixed;

            ProtoPartModuleSnapshot deployment =
                FindProtoModule(part, deploymentModuleName);

            if (deployment == null)
                return QuantumRelayDeploymentState.Missing;

            ReflectorDetection detection =
                ReflectorDetector.InspectUnloaded(
                    part,
                    deployment);

            return detection.Deployed
                ? QuantumRelayDeploymentState.Extended
                : QuantumRelayDeploymentState.Retracted;
        }

        private static bool IsPersistedOperationalState(
            string state)
        {
            return string.Equals(
                state,
                QuantumRelayOperationalState.Operational.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void ReadProtoElectricCharge(
            ProtoPartSnapshot part,
            GatewayCandidate result)
        {
            if (part.resources == null)
                return;

            foreach (ProtoPartResourceSnapshot resource in
                part.resources)
            {
                if (!string.Equals(
                        resource.resourceName,
                        "ElectricCharge",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.ElectricChargeAmount += resource.amount;
                result.ElectricChargeCapacity += resource.maxAmount;
            }
        }

        // ModuleQuantumRelay uses this shared vessel-level diagnostic.
        internal static bool HasCommNetCapability(
            Vessel vessel,
            out string evidence)
        {
            return TryGetCommNetCapability(vessel, out evidence);
        }

        private static bool TryGetCommNetCapability(
            Vessel vessel,
            out string evidence)
        {
            evidence = "none";

            if (vessel == null)
                return false;

            try
            {
                object connection = vessel.connection;

                if (connection == null)
                {
                    evidence =
                        "vessel.connection=null; " +
                        "checking transmitter fallback";
                }

                const BindingFlags flags =
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic;

                object comm =
                    connection != null
                        ? ReadMember(connection, "Comm", flags) ??
                          ReadMember(connection, "comm", flags)
                        : null;

                if (comm != null)
                {
                    object canRelay =
                        ReadMember(comm, "CanRelay", flags) ??
                        ReadMember(comm, "canRelay", flags);

                    if (canRelay is bool relay)
                    {
                        evidence = "Comm.CanRelay=" + relay;
                        return relay;
                    }

                    evidence =
                        "Comm=" + comm.GetType().FullName;
                    return true;
                }
            }
            catch (Exception exception)
            {
                evidence =
                    "CommNet reflection error: " +
                    exception.Message;
            }

            bool transmitter = HasAnyTransmitter(vessel);
            evidence = transmitter
                ? "ModuleDataTransmitter fallback"
                : evidence;

            return transmitter;
        }

        private static object ReadMember(
            object instance,
            string name,
            BindingFlags flags)
        {
            if (instance == null)
                return null;

            Type type = instance.GetType();
            PropertyInfo property =
                type.GetProperty(name, flags);

            if (property != null)
                return property.GetValue(instance, null);

            FieldInfo field =
                type.GetField(name, flags);

            return field != null
                ? field.GetValue(instance)
                : null;
        }

        internal static bool HasAnyTransmitter(Vessel vessel)
        {
            if (vessel == null)
                return false;

            if (vessel.loaded && vessel.parts != null)
            {
                return vessel.parts.Any(
                    part => FindTransmitter(part) != null);
            }

            return vessel.protoVessel != null &&
                   vessel.protoVessel.protoPartSnapshots != null &&
                   vessel.protoVessel.protoPartSnapshots.Any(
                       part => FindProtoTransmitter(part) != null);
        }

        internal static PartModule FindTransmitter(Part part)
        {
            return FindModule(
                part,
                QuantumRelaySettings.TransmitterModuleName);
        }

        internal static ProtoPartModuleSnapshot FindProtoTransmitter(
            ProtoPartSnapshot part)
        {
            return FindProtoModule(
                part,
                QuantumRelaySettings.TransmitterModuleName);
        }

        internal static string GetPartName(Part part)
        {
            if (part == null)
                return null;

            if (part.partInfo != null &&
                !string.IsNullOrEmpty(part.partInfo.name))
            {
                return part.partInfo.name;
            }

            return part.name;
        }

        internal static PartModule FindModule(
            Part part,
            string moduleName)
        {
            if (part == null || part.Modules == null)
                return null;

            return part.Modules
                .Cast<PartModule>()
                .FirstOrDefault(
                    module =>
                        module != null &&
                        string.Equals(
                            module.moduleName,
                            moduleName,
                            StringComparison.OrdinalIgnoreCase));
        }

        internal static ProtoPartModuleSnapshot FindProtoModule(
            ProtoPartSnapshot part,
            string moduleName)
        {
            if (part == null || part.modules == null)
                return null;

            return part.modules.FirstOrDefault(
                module =>
                    module != null &&
                    string.Equals(
                        module.moduleName,
                        moduleName,
                        StringComparison.OrdinalIgnoreCase));
        }

        internal static string DescribeRelevantHardware(
            Vessel vessel)
        {
            if (vessel == null)
                return "vessel=null";

            var lines = new List<string>();

            if (vessel.loaded && vessel.parts != null)
            {
                foreach (Part part in vessel.parts)
                {
                    if (part == null || part.Modules == null)
                        continue;

                    string[] modules = part.Modules
                        .Cast<PartModule>()
                        .Where(module => module != null)
                        .Select(module => module.moduleName)
                        .ToArray();

                    if (!modules.Any(IsRelevantModule))
                        continue;

                    lines.Add(
                        string.Format(
                            "part={0}; runtimeName={1}; " +
                            "title={2}; modules=[{3}]",
                            GetPartName(part),
                            part.name,
                            part.partInfo != null
                                ? part.partInfo.title
                                : string.Empty,
                            string.Join(",", modules)));
                }
            }
            else if (
                vessel.protoVessel != null &&
                vessel.protoVessel.protoPartSnapshots != null)
            {
                foreach (ProtoPartSnapshot part in
                    vessel.protoVessel.protoPartSnapshots)
                {
                    string[] modules =
                        part.modules != null
                            ? part.modules
                                .Where(module => module != null)
                                .Select(module => module.moduleName)
                                .ToArray()
                            : new string[0];

                    if (!modules.Any(IsRelevantModule))
                        continue;

                    lines.Add(
                        string.Format(
                            "protoPart={0}; modules=[{1}]",
                            part.partName,
                            string.Join(",", modules)));
                }
            }

            return lines.Count == 0
                ? "no relevant hardware modules found"
                : string.Join(" || ", lines.ToArray());
        }

        private static bool ReadProtoBool(
            ProtoPartModuleSnapshot module,
            string key,
            bool fallback)
        {
            string text =
                ReadProtoString(module, key, null);

            bool value;
            return bool.TryParse(text, out value)
                ? value
                : fallback;
        }

        private static int ReadProtoInt(
            ProtoPartModuleSnapshot module,
            string key,
            int fallback)
        {
            string text =
                ReadProtoString(module, key, null);

            int value;
            return int.TryParse(text, out value)
                ? value
                : fallback;
        }

        private static double ReadProtoDouble(
            ProtoPartModuleSnapshot module,
            string key,
            double fallback)
        {
            string text =
                ReadProtoString(module, key, null);

            double value;
            return double.TryParse(text, out value)
                ? value
                : fallback;
        }

        private static string ReadProtoString(
            ProtoPartModuleSnapshot module,
            string key,
            string fallback)
        {
            if (module == null || module.moduleValues == null)
                return fallback;

            string value = module.moduleValues.GetValue(key);

            return string.IsNullOrEmpty(value)
                ? fallback
                : value;
        }

        private static bool IsRelevantModule(
            string moduleName)
        {
            return string.Equals(
                       moduleName,
                       QuantumRelayConstants.QuantumRelayModuleName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       moduleName,
                       QuantumRelaySettings.ReflectorModuleName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       moduleName,
                       QuantumRelaySettings.TransmitterModuleName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       moduleName,
                       QuantumRelaySettings.CommandModuleName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       moduleName,
                       "ModuleDeployablePart",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsScannableVessel(Vessel vessel)
        {
            return vessel != null &&
                   vessel.state != Vessel.State.DEAD &&
                   vessel.vesselType != VesselType.Debris &&
                   vessel.vesselType != VesselType.EVA;
        }
    }
}
