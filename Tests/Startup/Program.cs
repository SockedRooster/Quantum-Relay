using System;
using QuantumRelay;
using QuantumRelay.Core;
using CommNet;

// Contract tests of production methods with stand-ins, not a Unity/KSP run.
public struct Vector3d
{
    public double x;
    public Vector3d(double value) { x = value; }
    public double magnitude => Math.Abs(x);
    public static double Distance(Vector3d a, Vector3d b) => Math.Abs(a.x - b.x);
}
public class CelestialBody
{
    public Vector3d position;
    public Vector3d GetWorldSurfacePosition(double lat, double lon, double alt) => new Vector3d(position.x + alt);
}
public class Orbit
{
    public CelestialBody referenceBody;
    public double relative;
    public Vector3d getRelativePositionAtUT(double ut) => new Vector3d(relative + ut);
    public Vector3d getPositionAtUT(double ut) => new Vector3d(referenceBody.position.x + relative + ut);
}
public static class Planetarium { public static double GetUniversalTime() => 10; }
public class Vessel
{
    public Guid id = Guid.NewGuid();
    public bool loaded, LandedOrSplashed;
    public double latitude, longitude, altitude;
    public CelestialBody mainBody;
    public Orbit orbit;
    public Vector3d cached;
    public CommNetVessel connection;
    public Vector3d GetWorldPos3D() => cached;
}
namespace UnityEngine { public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } } }
namespace CommNet
{
    public class CommNode { }
    public class CommNetVessel { public CommNode Comm; }
    public class CommNetNetwork
    {
        public static CommNetNetwork Instance = new CommNetNetwork();
        public int rebuilds;
        public void QueueRebuild() { rebuilds++; }
    }
}
namespace QuantumRelay
{
    internal static class QuantumRelaySettings { public static bool AutoRebuildCommNet = true; }
    internal class GatewayCandidate
    {
        public Vessel Vessel;
        public bool HasQuantumRelayModule = true;
        public double RelaySignalStrength = .6;
        public int RelayTier = 2;
    }
}
namespace QuantumRelay.Core
{
    internal class ActiveQuantumLink
    {
        public string Id, DisplayName, Reason;
        public GatewayCandidate GatewayA, GatewayB;
        public bool Online;
        public bool HasValidGateways => GatewayA != null && GatewayB != null;
    }
}
internal static class Program
{
    static int count;
    static void Check(string name, bool ok)
    {
        if (!ok) throw new Exception(name);
        Console.WriteLine("PASS " + name); count++;
    }
    static void Main()
    {
        var body = new CelestialBody { position = new Vector3d(1e20) };
        var v = new Vessel { orbit = new Orbit { referenceBody = body, relative = 140000 }, cached = new Vector3d(0) };
        double d;
        Check("unloaded gateway uses orbit at current UT despite stale physics", GatewayDistance.TryMeasure(v, body, out d) && d == 140010);
        Check("null vessel rejected", !GatewayDistance.TryMeasure(null, body, out d));
        Check("null endpoint rejected", !GatewayDistance.TryMeasure(v, null, out d));
        v.orbit = null;
        Check("missing orbit waits rather than using stale CoMD", !GatewayDistance.TryMeasure(v, body, out d));
        body.position = new Vector3d(1000); v.loaded = true; v.cached = new Vector3d(1050);
        Check("loaded vessel uses live physics", GatewayDistance.TryMeasure(v, body, out d) && d == 50);
        v.loaded = false; v.LandedOrSplashed = true; v.mainBody = body; v.altitude = 100;
        Check("unloaded landed vessel uses surface coordinates", GatewayDistance.TryMeasure(v, body, out d) && d == 100);
        v.LandedOrSplashed = false; v.orbit = new Orbit { referenceBody = body, relative = 100 };
        var other = new CelestialBody { position = new Vector3d(2000) };
        Check("different reference body uses world orbit position", GatewayDistance.TryMeasure(v, other, out d) && d == 890);
        v.orbit.relative = double.NaN;
        Check("invalid orbit rejected", !GatewayDistance.TryMeasure(v, body, out d));
        v.orbit.relative = double.PositiveInfinity;
        Check("infinite orbit rejected", !GatewayDistance.TryMeasure(v, body, out d));
        var a = new Vessel { connection = new CommNetVessel { Comm = new CommNode() } };
        var b = new Vessel { connection = new CommNetVessel { Comm = new CommNode() } };
        var link = new ActiveQuantumLink { Id = "Debdeb", Online = true, GatewayA = new GatewayCandidate { Vessel = a }, GatewayB = new GatewayCandidate { Vessel = b } };
        var links = new[] { link };
        QuantumGatewayManager.SetActiveLinks(links);
        Check("first pair queues rebuild", CommNetNetwork.Instance.rebuilds == 1);
        QuantumGatewayManager.SetActiveLinks(links);
        Check("unchanged pair does not rebuild repeatedly", CommNetNetwork.Instance.rebuilds == 1);
        var oldA = a.connection.Comm; a.connection.Comm = new CommNode();
        QuantumGatewayManager.SetActiveLinks(links);
        Check("replacement node queues rebuild with unchanged vessel IDs", CommNetNetwork.Instance.rebuilds == 2);
        Check("old nodes no longer accepted", !QuantumGatewayManager.IsGatewayPair(oldA, b.connection.Comm));
        Check("new nodes registered", QuantumGatewayManager.IsGatewayPair(a.connection.Comm, b.connection.Comm));
        b.connection.Comm = null; QuantumGatewayManager.SetActiveLinks(links);
        Check("missing node removes pair", QuantumGatewayManager.ActivePairCount == 0 && CommNetNetwork.Instance.rebuilds == 3);
        b.connection.Comm = new CommNode(); QuantumGatewayManager.SetActiveLinks(links);
        Check("late node initialization restores pair", QuantumGatewayManager.ActivePairCount == 1 && CommNetNetwork.Instance.rebuilds == 4);
        link.Online = false; QuantumGatewayManager.SetActiveLinks(links);
        Check("offline hardware is never forced online", QuantumGatewayManager.ActivePairCount == 0);
        QuantumRelaySettings.AutoRebuildCommNet = false;
        var before = CommNetNetwork.Instance.rebuilds;
        link.Online = true; QuantumGatewayManager.SetActiveLinks(links);
        Check("automatic rebuild toggle respected", CommNetNetwork.Instance.rebuilds == before);
        QuantumGatewayManager.QueueRebuildNow();
        Check("manual rebuild works when automatic disabled", CommNetNetwork.Instance.rebuilds == before + 1);
        Console.WriteLine(count + " startup contract checks passed.");
    }
}
