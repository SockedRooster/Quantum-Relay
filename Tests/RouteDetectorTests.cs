using System;
using System.Collections.Generic;
using QuantumRelay;
using CommNet;

// Contract fixture, not KSP simulation. Compile the actual production detector
// with small stand-ins for the KSP graph API; never instantiate Unity objects.
public class Vessel { public CommNetVessel connection; }
namespace CommNet
{
    public class CommNode { }
    public class CommLink { public CommNode a, b; }
    public class CommPath : List<CommLink> { }
    public class CommNetVessel
    {
        public bool IsConnected;
        public CommPath ControlPath;
    }
}
namespace QuantumRelay
{
    internal class QuantumCommNetwork { public List<CommLink> Links = new List<CommLink>(); }
    internal static class QuantumGatewayManager
    {
        public static CommNode A, B;
        public static CommNode OtherA, OtherB;
        public static bool IsGatewayPair(CommNode a, CommNode b)
        {
            return (A != null && B != null &&
                ((ReferenceEquals(a, A) && ReferenceEquals(b, B)) ||
                 (ReferenceEquals(a, B) && ReferenceEquals(b, A)))) ||
                (OtherA != null && OtherB != null &&
                ((ReferenceEquals(a, OtherA) && ReferenceEquals(b, OtherB)) ||
                 (ReferenceEquals(a, OtherB) && ReferenceEquals(b, OtherA))));
        }
    }
}
internal static class Program
{
    private static int count;
    private static void Check(string name, bool expected, Vessel vessel, QuantumCommNetwork graph)
    {
        bool actual = QuantumRouteDetector.UsesQuantumRoute(vessel, graph);
        if (actual != expected) throw new Exception(name + ": expected " + expected + ", got " + actual);
        Console.WriteLine("PASS " + name);
        count++;
    }
    public static void Main()
    {
        var a = new CommNode(); var b = new CommNode(); var craft = new CommNode();
        var home = new CommNode(); var graph = new QuantumCommNetwork();
        var quantum = new CommLink { a = a, b = b };
        var uplink = new CommLink { a = craft, b = a };
        var downlink = new CommLink { a = b, b = home };
        graph.Links.AddRange(new[] { uplink, quantum, downlink });
        QuantumGatewayManager.A = a; QuantumGatewayManager.B = b;
        var path = new CommPath();
        var connection = new CommNetVessel { IsConnected = true, ControlPath = path };
        var vessel = new Vessel { connection = connection };
        Check("no vessel", false, null, graph);
        Check("no quantum graph", false, vessel, null);
        Check("no connection", false, new Vessel(), graph);
        Check("no path", false, new Vessel { connection = new CommNetVessel { IsConnected = true } }, graph);
        Check("empty path with online pair", false, vessel, graph);
        path.AddRange(new[] { uplink, quantum, downlink });
        connection.IsConnected = false;
        Check("disconnected craft with stale path", false, vessel, graph);
        connection.IsConnected = true;
        Check("quantum hop in multi-hop route", true, vessel, graph);
        path.Clear();
        path.AddRange(new[] {
            new CommLink { a = craft, b = a },
            new CommLink { a = a, b = b },
            new CommLink { a = b, b = home }
        });
        Check("KSP-style copied multi-hop route", true, vessel, graph);
        path[1] = new CommLink { a = b, b = a };
        Check("copied reversed quantum hop", true, vessel, graph);
        path.Clear(); path.AddRange(new[] { uplink, quantum, downlink });
        quantum.a = b; quantum.b = a;
        Check("reverse gateway direction", true, vessel, graph);
        path.Clear(); path.Add(uplink);
        Check("visiting gateway alone", false, vessel, graph);
        path.Add(quantum);
        QuantumGatewayManager.A = null;
        Check("gateway pair went offline", false, vessel, graph);
        QuantumGatewayManager.A = a;
        var replacement = new CommLink { a = b, b = a };
        graph.Links.Remove(quantum); graph.Links.Add(replacement);
        Check("replacement link retains same live nodes", true, vessel, graph);
        graph.Links.Remove(replacement);
        Check("removed graph edge despite copied path", false, vessel, graph);
        var rebuilt = new CommLink { a = new CommNode(), b = new CommNode() };
        graph.Links.Add(rebuilt);
        Check("old node identities after graph rebuild", false, vessel, graph);
        graph.Links.Remove(rebuilt); graph.Links.Add(replacement);
        path[1] = replacement;
        Check("current rebuilt quantum link", true, vessel, graph);
        graph.Links.Remove(uplink);
        Check("stale ordinary hop invalidates whole route", false, vessel, graph);
        graph.Links.Add(uplink);
        path.Add(null);
        Check("null route edge", false, vessel, graph);
        path.RemoveAt(path.Count - 1);
        replacement.a = null;
        Check("null edge endpoint", false, vessel, graph);
        replacement.a = b;
        var direct = new CommLink { a = craft, b = home }; graph.Links.Add(direct);
        var other = new Vessel { connection = new CommNetVessel {
            IsConnected = true, ControlPath = new CommPath { direct } } };
        Check("switch to direct-connected craft", false, other, graph);
        Check("switch back to quantum craft", true, vessel, graph);
        CheckPair("selected network pair", true, vessel, graph, a, b);
        CheckPair("reversed selected pair", true, vessel, graph, b, a);
        CheckPair("ordinary hop", false, vessel, graph, craft, a);
        CheckPair("missing endpoint", false, vessel, graph, null, b);
        CheckPair("ordinary craft", false, other, graph, a, b);
        var extraA = new CommNode(); var extraB = new CommNode();
        QuantumGatewayManager.OtherA = extraA; QuantumGatewayManager.OtherB = extraB;
        graph.Links.Add(new CommLink { a = extraA, b = extraB });
        CheckPair("other online network not on route", false, vessel, graph, extraA, extraB);
        graph.Links.Remove(uplink);
        CheckPair("stale route cannot highlight pair", false, vessel, graph, a, b);
        graph.Links.Add(uplink);
        Console.WriteLine(count + " route detector contract tests passed.");
        SettingsNodeTests.Run();
    }

    private static void CheckPair(string name, bool expected, Vessel vessel,
        QuantumCommNetwork graph, CommNode a, CommNode b)
    {
        if (QuantumRouteDetector.UsesPair(vessel, graph, a, b) != expected)
            throw new Exception("Pair selection failed: " + name);
        Console.WriteLine("PASS pair " + name); count++;
    }
}
