using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using QuantumRelay;

// Production settings code with a JSON-backed stand-in for ConfigNode.
// Exercises actual disk I/O and settings serialization, not KSP's CFG parser.
public class ConfigNode
{
    public string name { get; set; }
    public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
    public List<ConfigNode> Children { get; set; } = new List<ConfigNode>();
    public static bool FailSave;
    public ConfigNode() { }
    public ConfigNode(string n) { name = n; }
    public void AddValue(string key, object value) { Values[key] = Convert.ToString(value, CultureInfo.InvariantCulture); }
    public string GetValue(string key) => Values.TryGetValue(key, out var value) ? value : null;
    public void AddNode(ConfigNode node) { Children.Add(node); }
    public ConfigNode GetNode(string n) => Children.Find(c => c.name == n);
    public bool Save(string path)
    {
        if (FailSave) return false;
        File.WriteAllText(path, JsonSerializer.Serialize(this)); return true;
    }
    public static ConfigNode Load(string path) => JsonSerializer.Deserialize<ConfigNode>(File.ReadAllText(path));
}
public static class KSPUtil { public static string ApplicationRootPath; }
namespace UnityEngine { public static class Debug { public static void Log(object x) {} public static void LogWarning(object x) {} public static void LogError(object x) {} } }
namespace QuantumRelay { internal static class QuantumRelayConstants
{
    public const double DefaultGatewayRadiusMetres = 250000;
    public const string WormholeTag="Wormhole", WormholeA="A", WormholeB="B", LegacyReflectorModuleName="Reflector", TransmitterModuleName="Transmitter", CommandModuleName="Command", ReflectorAnimationName="Extend";
} }
internal static class Program
{
    static int count;
    static void Check(string label, bool pass) { if (!pass) throw new Exception(label); Console.WriteLine("PASS " + label); count++; }
    static string PathName => Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "QuantumRelay", "PluginData", "Settings.cfg");
    static void Main()
    {
        // Caller supplies a workspace output folder; never write into the game.
        KSPUtil.ApplicationRootPath = Path.Combine(AppContext.BaseDirectory, "fixture-output");
        Directory.CreateDirectory(Path.GetDirectoryName(PathName));
        QuantumRelaySettings.ResetDefaults(true);
        Check("defaults written", QuantumRelaySettings.LastSaveSucceeded && File.Exists(PathName));
        Check("default badge and scale", QuantumRelaySettings.ShowFlightHud && QuantumRelaySettings.HudFollowStock && QuantumRelaySettings.UiScale == 1f);
        QuantumRelaySettings.ApplyDisplay(false, false, 350, 180, 1.25f);
        QuantumRelaySettings.Apply(false, false, true, true);
        QuantumRelaySettings.SaveWindowLayout(45, 55, 900, 600);
        QuantumRelaySettings.ResetDefaults(false);
        QuantumRelaySettings.Load();
        Check("display preferences survive reset and disk reload", !QuantumRelaySettings.ShowFlightHud && !QuantumRelaySettings.HudFollowStock && QuantumRelaySettings.HudX == 350 && QuantumRelaySettings.HudY == 180 && QuantumRelaySettings.UiScale == 1.25f);
        Check("network toggles survive disk reload", !QuantumRelaySettings.AutoRebuildCommNet && !QuantumRelaySettings.ShowScreenMessages && QuantumRelaySettings.DebugLogging);
        Check("window layout survives disk reload", QuantumRelaySettings.WindowX == 45 && QuantumRelaySettings.WindowY == 55 && QuantumRelaySettings.WindowWidth == 900 && QuantumRelaySettings.WindowHeight == 600);
        QuantumRelaySettings.ApplyDisplay(true, true, -5, -10, 9);
        Check("display range upper limit and nonnegative coordinates", QuantumRelaySettings.HudX == 0 && QuantumRelaySettings.HudY == 0 && QuantumRelaySettings.UiScale == 1.5f);
        QuantumRelaySettings.ApplyDisplay(true, true, float.NaN, float.PositiveInfinity, float.NaN);
        Check("nonfinite display data repaired", QuantumRelaySettings.HudX == 270 && QuantumRelaySettings.HudY == 84 && QuantumRelaySettings.UiScale == 1f);
        QuantumRelaySettings.ApplyDisplay(true, true, 0, 0, .1f);
        Check("scale lower limit", QuantumRelaySettings.UiScale == .75f);
        var old = new ConfigNode("QUANTUM_RELAY_CONFIG"); var node = new ConfigNode("QUANTUM_RELAY_SETTINGS"); old.AddNode(node);
        node.AddValue("windowWidth", 800); node.AddValue("windowHeight", 500); old.Save(PathName);
        QuantumRelaySettings.Load();
        Check("older configs retain saved layout", QuantumRelaySettings.WindowWidth == 800 && QuantumRelaySettings.WindowHeight == 500);
        Check("older configs gain safe display defaults", QuantumRelaySettings.ShowFlightHud && QuantumRelaySettings.HudFollowStock && QuantumRelaySettings.UiScale == 1f);
        node.AddValue("windowWidth", "NaN"); node.AddValue("windowHeight", "Infinity"); node.AddValue("uiScale", "NaN"); old.Save(PathName);
        QuantumRelaySettings.Load();
        Check("invalid saved dimensions repaired", QuantumRelaySettings.WindowWidth == 560 && QuantumRelaySettings.WindowHeight == 700 && QuantumRelaySettings.UiScale == 1f);
        QuantumRelaySettings.SaveWindowLayout(-1, -1, 5, 10);
        Check("minimum layout bounds", QuantumRelaySettings.WindowX == 0 && QuantumRelaySettings.WindowY == 0 && QuantumRelaySettings.WindowWidth == 460 && QuantumRelaySettings.WindowHeight == 420);
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            QuantumRelaySettings.ApplyDisplay(true, false, 300.5f, 90.5f, 1.15f); QuantumRelaySettings.Save(); QuantumRelaySettings.Load();
            Check("settings use invariant numeric format", QuantumRelaySettings.HudX == 300.5f && QuantumRelaySettings.UiScale == 1.15f);
        }
        finally { CultureInfo.CurrentCulture = culture; }
        ConfigNode.FailSave = true; QuantumRelaySettings.Save();
        Check("failed write reported", !QuantumRelaySettings.LastSaveSucceeded);
        ConfigNode.FailSave = false; QuantumRelaySettings.ResetDefaults(true); QuantumRelaySettings.Load();
        Check("reset defaults saved and restored", QuantumRelaySettings.LastSaveSucceeded && QuantumRelaySettings.ShowFlightHud && QuantumRelaySettings.HudFollowStock && QuantumRelaySettings.WindowWidth == 560 && QuantumRelaySettings.UiScale == 1f);
        Console.WriteLine(count + " settings contract checks passed.");
    }
}
