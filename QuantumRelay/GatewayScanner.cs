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
        public bool HasReflector { get; set; }
        public bool ReflectorDeployed { get; set; }
        public string ReflectorEvidence { get; set; }
        public bool HasCommNet { get; set; }
        public string CommNetEvidence { get; set; }
        public bool HasProbeControl { get; set; }
        public double ElectricChargeAmount { get; set; }
        public double ElectricChargeCapacity { get; set; }
        public bool HasElectricCharge => ElectricChargeAmount > 0.01;
        public bool IsValid => Vessel != null && Wormhole != null && HasReflector && ReflectorDeployed && HasCommNet && HasProbeControl && HasElectricCharge;
        public string StatusKey => string.Join("|", IsValid, HasReflector, ReflectorDeployed, HasCommNet, HasProbeControl, HasElectricCharge, ReflectorEvidence ?? string.Empty, CommNetEvidence ?? string.Empty);
    }

    internal static class GatewayScanner
    {
        public static List<GatewayCandidate> FindCandidates(IEnumerable<WormholeInfo> wormholes)
        {
            var results = new List<GatewayCandidate>();
            if (FlightGlobals.Vessels == null || wormholes == null) return results;

            foreach (WormholeInfo wormhole in wormholes)
            {
                if (wormhole?.Body == null) continue;
                foreach (Vessel vessel in FlightGlobals.Vessels.Where(IsScannableVessel))
                {
                    double distance;
                    try { distance = Vector3d.Distance(vessel.GetWorldPos3D(), wormhole.Body.position); }
                    catch { continue; }
                    if (distance > QuantumRelaySettings.GatewayRadiusMetres) continue;
                    results.Add(vessel.loaded ? InspectLoaded(vessel, wormhole, distance) : InspectUnloaded(vessel, wormhole, distance));
                }
            }
            return results;
        }

        public static GatewayCandidate RefreshCandidate(GatewayCandidate previous)
        {
            if (previous?.Vessel == null || previous.Wormhole?.Body == null) return null;
            Vessel vessel = previous.Vessel;
            if (!IsScannableVessel(vessel)) return null;

            double distance;
            try { distance = Vector3d.Distance(vessel.GetWorldPos3D(), previous.Wormhole.Body.position); }
            catch { return null; }

            if (distance > QuantumRelaySettings.GatewayRadiusMetres)
                return NewCandidate(vessel, previous.Wormhole, distance);

            return vessel.loaded ? InspectLoaded(vessel, previous.Wormhole, distance) : InspectUnloaded(vessel, previous.Wormhole, distance);
        }

        private static GatewayCandidate NewCandidate(Vessel vessel, WormholeInfo wormhole, double distance)
        {
            return new GatewayCandidate
            {
                Vessel = vessel,
                Wormhole = wormhole,
                DistanceMetres = distance,
                IsLoaded = vessel.loaded,
                ReflectorEvidence = "not found",
                CommNetEvidence = "not found"
            };
        }

        private static GatewayCandidate InspectLoaded(Vessel vessel, WormholeInfo wormhole, double distance)
        {
            GatewayCandidate result = NewCandidate(vessel, wormhole, distance);
            if (vessel.parts != null)
            {
                foreach (Part part in vessel.parts)
                {
                    PartModule reflectorModule = FindModule(part, QuantumRelaySettings.ReflectorModuleName);
                    if (reflectorModule != null)
                    {
                        ReflectorDetection detection = ReflectorDetector.InspectLoaded(part, reflectorModule);
                        result.HasReflector = true;
                        result.ReflectorDeployed = detection.Deployed;
                        result.ReflectorEvidence = detection.Evidence;
                    }
                    if (FindModule(part, QuantumRelaySettings.CommandModuleName) != null)
                        result.HasProbeControl = true;
                }
            }

            result.HasCommNet = TryGetCommNetCapability(vessel, out string evidence);
            result.CommNetEvidence = evidence;
            vessel.GetConnectedResourceTotals(PartResourceLibrary.ElectricityHashcode, out double amount, out double capacity);
            result.ElectricChargeAmount = amount;
            result.ElectricChargeCapacity = capacity;
            return result;
        }

        private static GatewayCandidate InspectUnloaded(Vessel vessel, WormholeInfo wormhole, double distance)
        {
            GatewayCandidate result = NewCandidate(vessel, wormhole, distance);
            ProtoVessel proto = vessel.protoVessel;
            if (proto?.protoPartSnapshots != null)
            {
                foreach (ProtoPartSnapshot part in proto.protoPartSnapshots)
                {
                    ProtoPartModuleSnapshot reflectorModule = FindProtoModule(part, QuantumRelaySettings.ReflectorModuleName);
                    if (reflectorModule != null)
                    {
                        ReflectorDetection detection = ReflectorDetector.InspectUnloaded(part, reflectorModule);
                        result.HasReflector = true;
                        result.ReflectorDeployed = detection.Deployed;
                        result.ReflectorEvidence = detection.Evidence;
                    }
                    if (FindProtoModule(part, QuantumRelaySettings.CommandModuleName) != null)
                        result.HasProbeControl = true;

                    if (part.resources == null) continue;
                    foreach (ProtoPartResourceSnapshot resource in part.resources)
                    {
                        if (!string.Equals(resource.resourceName, "ElectricCharge", StringComparison.OrdinalIgnoreCase)) continue;
                        result.ElectricChargeAmount += resource.amount;
                        result.ElectricChargeCapacity += resource.maxAmount;
                    }
                }
            }

            result.HasCommNet = TryGetCommNetCapability(vessel, out string evidence);
            result.CommNetEvidence = evidence;
            return result;
        }

        // v0.7 no longer tries to recognise a particular relay antenna or Near Future feed.
        // It asks the vessel's own CommNet objects whether the vessel participates in CommNet.
        private static bool TryGetCommNetCapability(Vessel vessel, out string evidence)
        {
            evidence = "none";
            if (vessel == null) return false;
            try
            {
                object connection = vessel.connection;
                if (connection == null)
                {
                    evidence = "vessel.connection=null";
                    return false;
                }

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                object comm = ReadMember(connection, "Comm", flags) ?? ReadMember(connection, "comm", flags);
                if (comm != null)
                {
                    object canRelay = ReadMember(comm, "CanRelay", flags) ?? ReadMember(comm, "canRelay", flags);
                    if (canRelay is bool relay)
                    {
                        evidence = "Comm.CanRelay=" + relay;
                        return relay;
                    }
                    evidence = "Comm=" + comm.GetType().FullName;
                    return true;
                }

            }
            catch (Exception ex)
            {
                evidence = "CommNet reflection error: " + ex.Message;
            }

            // Final compatibility fallback: any stock transmitter means the vessel can be
            // promoted into a bridge endpoint. This is not used to classify a specific relay part.
            bool transmitter = HasAnyTransmitter(vessel);
            evidence = transmitter ? "ModuleDataTransmitter fallback" : evidence;
            return transmitter;
        }

        private static object ReadMember(object instance, string name, BindingFlags flags)
        {
            if (instance == null) return null;
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null) return property.GetValue(instance, null);
            FieldInfo field = type.GetField(name, flags);
            return field?.GetValue(instance);
        }

        internal static bool HasAnyTransmitter(Vessel vessel)
        {
            if (vessel == null) return false;
            if (vessel.loaded && vessel.parts != null)
                return vessel.parts.Any(p => FindTransmitter(p) != null);
            return vessel.protoVessel?.protoPartSnapshots != null && vessel.protoVessel.protoPartSnapshots.Any(p => FindProtoTransmitter(p) != null);
        }

        internal static PartModule FindTransmitter(Part part)
        {
            return FindModule(part, QuantumRelaySettings.TransmitterModuleName);
        }

        internal static ProtoPartModuleSnapshot FindProtoTransmitter(ProtoPartSnapshot part)
        {
            return FindProtoModule(part, QuantumRelaySettings.TransmitterModuleName);
        }

        internal static string GetPartName(Part part) => part?.partInfo != null && !string.IsNullOrEmpty(part.partInfo.name) ? part.partInfo.name : part?.name;

        internal static PartModule FindModule(Part part, string moduleName)
        {
            return part?.Modules == null ? null : part.Modules.Cast<PartModule>()
                .FirstOrDefault(m => m != null && string.Equals(m.moduleName, moduleName, StringComparison.OrdinalIgnoreCase));
        }

        internal static ProtoPartModuleSnapshot FindProtoModule(ProtoPartSnapshot part, string moduleName)
        {
            return part?.modules?.FirstOrDefault(m => m != null && string.Equals(m.moduleName, moduleName, StringComparison.OrdinalIgnoreCase));
        }

        internal static string DescribeRelevantHardware(Vessel vessel)
        {
            if (vessel == null) return "vessel=null";
            var lines = new List<string>();
            if (vessel.loaded && vessel.parts != null)
            {
                foreach (Part part in vessel.parts)
                {
                    if (part?.Modules == null) continue;
                    string[] modules = part.Modules.Cast<PartModule>().Where(m => m != null).Select(m => m.moduleName).ToArray();
                    if (!modules.Any(IsRelevantModule)) continue;
                    lines.Add(string.Format("part={0}; runtimeName={1}; title={2}; modules=[{3}]",
                        GetPartName(part), part.name, part.partInfo?.title ?? "", string.Join(",", modules)));
                }
            }
            else if (vessel.protoVessel?.protoPartSnapshots != null)
            {
                foreach (ProtoPartSnapshot part in vessel.protoVessel.protoPartSnapshots)
                {
                    string[] modules = part.modules?.Where(m => m != null).Select(m => m.moduleName).ToArray() ?? new string[0];
                    if (!modules.Any(IsRelevantModule)) continue;
                    lines.Add(string.Format("protoPart={0}; modules=[{1}]", part.partName, string.Join(",", modules)));
                }
            }
            return lines.Count == 0 ? "no relevant hardware modules found" : string.Join(" || ", lines.ToArray());
        }

        private static bool IsRelevantModule(string moduleName)
        {
            return string.Equals(moduleName, QuantumRelaySettings.ReflectorModuleName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(moduleName, QuantumRelaySettings.TransmitterModuleName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(moduleName, QuantumRelaySettings.CommandModuleName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(moduleName, "ModuleDeployablePart", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsScannableVessel(Vessel vessel) => vessel != null && vessel.state != Vessel.State.DEAD && vessel.vesselType != VesselType.Debris && vessel.vesselType != VesselType.EVA;
    }
}
