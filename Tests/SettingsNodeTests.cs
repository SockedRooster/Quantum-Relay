using System;
using System.Collections.Generic;
using QuantumRelay;

// Minimal node-tree contract fixture; this is not the KSP file parser.
public class ConfigNode
{
    public string name;
    private readonly List<ConfigNode> children = new List<ConfigNode>();
    public ConfigNode(string name = "") { this.name = name; }
    public void AddNode(ConfigNode node) { children.Add(node); }
    public ConfigNode GetNode(string target) { return children.Find(n => n.name == target); }
}
internal static class SettingsNodeTests
{
    private static int count;
    private static void Check(string name, ConfigNode root, ConfigNode expected)
    {
        if (!ReferenceEquals(QuantumRelaySettingsNode.Find(root), expected))
            throw new Exception("Settings node resolver failed: " + name);
        Console.WriteLine("PASS settings " + name); count++;
    }
    public static void Run()
    {
        var settings = new ConfigNode("QUANTUM_RELAY_SETTINGS");
        Check("null root", null, null);
        Check("direct root", settings, settings);
        var root = new ConfigNode(); root.AddNode(settings);
        Check("direct child as loaded by KSP", root, settings);
        var wrapper = new ConfigNode("QUANTUM_RELAY_CONFIG"); wrapper.AddNode(settings);
        Check("wrapper root", wrapper, settings);
        var savedFile = new ConfigNode(); savedFile.AddNode(wrapper);
        Check("wrapped child matching Save format", savedFile, settings);
        Check("missing settings", new ConfigNode(), null);
        var wrong = new ConfigNode(); wrong.AddNode(new ConfigNode("OTHER_MOD"));
        Check("unrelated node", wrong, null);
        Console.WriteLine(count + " settings-node contract tests passed.");
    }
}
