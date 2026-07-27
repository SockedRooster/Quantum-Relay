using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// User-adjustable Quantum Relay settings. Values are stored outside the
    /// GameDatabase in PluginData so saving does not trigger ModuleManager or
    /// GameDatabase reload behaviour.
    /// </summary>
    internal static class QuantumRelaySettings
    {
        public const double DefaultGatewayRadiusMetres = QuantumRelayConstants.DefaultGatewayRadiusMetres;

        public static double GatewayRadiusMetres { get; private set; } = DefaultGatewayRadiusMetres;
        public static bool AutoRebuildCommNet { get; private set; } = true;
        public static bool ShowScreenMessages { get; private set; } = true;
        public static bool DebugLogging { get; private set; }
        public static float WindowX { get; private set; } = 260f;
        public static float WindowY { get; private set; } = 100f;
        public static float WindowWidth { get; private set; } = 560f;
        public static float WindowHeight { get; private set; } = 700f;

        // Expensive work is deliberately slow and event-driven.
        public const double FullGatewayScanIntervalSeconds = 15.0;
        public const double DirtyScanDebounceSeconds = 1.0;
        public const double GatewayMaintenanceIntervalSeconds = 1.0;
        public const double DetailedLogIntervalSeconds = 30.0;
        public const double EdgeRetryIntervalSeconds = 2.0;

        public const string WormholeTag = QuantumRelayConstants.WormholeTag;
        public const string WormholeA = QuantumRelayConstants.WormholeA;
        public const string WormholeB = QuantumRelayConstants.WormholeB;
        public const string Rfl2000PartName = "nfex-antenna-reflector-huge-1";
        public const string FraRelayPartName = "nfex-antenna-feeder-relay-1";
        public const string ReflectorModuleName = QuantumRelayConstants.LegacyReflectorModuleName;
        public const string FeedModuleName = "ModuleAntennaFeed";
        public const string TransmitterModuleName = QuantumRelayConstants.TransmitterModuleName;
        public const string CommandModuleName = QuantumRelayConstants.CommandModuleName;
        public const string ReflectorAnimationName = QuantumRelayConstants.ReflectorAnimationName;

        private static bool _missingNodeWarningLogged;

        private static string SettingsPath => Path.Combine(
            KSPUtil.ApplicationRootPath,
            "GameData", "QuantumRelay", "PluginData", "Settings.cfg");

        public static void Load()
        {
            ResetDefaults(false);

            try
            {
                if (!File.Exists(SettingsPath))
                {
                    Save();
                    Debug.Log("[QuantumRelay] Created default settings at " + SettingsPath);
                    return;
                }

                ConfigNode root = ConfigNode.Load(SettingsPath);
                ConfigNode node = root != null && root.name == "QUANTUM_RELAY_SETTINGS"
                    ? root
                    : root?.GetNode("QUANTUM_RELAY_SETTINGS");

                if (node == null)
                {
                    if (!_missingNodeWarningLogged)
                    {
                        _missingNodeWarningLogged = true;
                        Debug.LogWarning(
                            "[QuantumRelay] Settings node missing; recreated defaults.");
                    }

                    Save();
                    return;
                }

                GatewayRadiusMetres = SanitizeRadius(ReadDouble(node, "gatewayRadiusMetres", DefaultGatewayRadiusMetres));
                AutoRebuildCommNet = ReadBool(node, "autoRebuildCommNet", true);
                ShowScreenMessages = ReadBool(node, "showScreenMessages", true);
                DebugLogging = ReadBool(node, "debugLogging", false);
                WindowX = ReadFloat(node, "windowX", 260f);
                WindowY = ReadFloat(node, "windowY", 100f);
                WindowWidth = Math.Max(
                    460f,
                    ReadFloat(node, "windowWidth", 560f));
                WindowHeight = Math.Max(
                    420f,
                    ReadFloat(node, "windowHeight", 700f));

                Debug.Log("[QuantumRelay] Settings loaded | radius=" +
                          GatewayRadiusMetres.ToString("0", CultureInfo.InvariantCulture) + "m");
            }
            catch (Exception ex)
            {
                ResetDefaults(false);
                Debug.LogWarning("[QuantumRelay] Failed to load settings; defaults restored: " + ex.Message);
            }
        }

        public static bool Apply(double radiusMetres, bool autoRebuild,
            bool showMessages, bool debugLogging, bool save)
        {
            double oldRadius = GatewayRadiusMetres;
            bool oldAuto = AutoRebuildCommNet;

            GatewayRadiusMetres = SanitizeRadius(radiusMetres);
            AutoRebuildCommNet = autoRebuild;
            ShowScreenMessages = showMessages;
            DebugLogging = debugLogging;

            if (save)
                Save();

            return Math.Abs(oldRadius - GatewayRadiusMetres) > 0.01 ||
                   oldAuto != AutoRebuildCommNet;
        }

        public static void ResetDefaults(bool save)
        {
            GatewayRadiusMetres = DefaultGatewayRadiusMetres;
            AutoRebuildCommNet = true;
            ShowScreenMessages = true;
            DebugLogging = false;
            WindowX = 260f;
            WindowY = 100f;
            WindowWidth = 560f;
            WindowHeight = 700f;
            if (save) Save();
        }

        public static void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                ConfigNode node = new ConfigNode("QUANTUM_RELAY_SETTINGS");
                node.AddValue("gatewayRadiusMetres", GatewayRadiusMetres.ToString("0", CultureInfo.InvariantCulture));
                node.AddValue("autoRebuildCommNet", AutoRebuildCommNet);
                node.AddValue("showScreenMessages", ShowScreenMessages);
                node.AddValue("debugLogging", DebugLogging);
                node.AddValue("windowX", WindowX.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("windowY", WindowY.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("windowWidth", WindowWidth.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("windowHeight", WindowHeight.ToString("0.##", CultureInfo.InvariantCulture));

                ConfigNode root = new ConfigNode("QUANTUM_RELAY_CONFIG");
                root.AddNode(node);
                root.Save(SettingsPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QuantumRelay] Failed to save settings: " + ex.Message);
            }
        }


        public static void SaveWindowPosition(float x, float y)
        {
            SaveWindowLayout(x, y, WindowWidth, WindowHeight);
        }

        public static void SaveWindowLayout(
            float x,
            float y,
            float width,
            float height)
        {
            WindowX = Math.Max(0f, x);
            WindowY = Math.Max(0f, y);
            WindowWidth = Math.Max(460f, width);
            WindowHeight = Math.Max(420f, height);
            Save();
        }

        private static double SanitizeRadius(double value)
        {
            value = Math.Max(100000.0, Math.Min(500000.0, value));
            return Math.Round(value / 25000.0, MidpointRounding.AwayFromZero) * 25000.0;
        }

        private static int ReadInt(ConfigNode node, string key, int fallback)
        {
            int value;
            return int.TryParse(node.GetValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static double ReadDouble(ConfigNode node, string key, double fallback)
        {
            double value;
            return double.TryParse(node.GetValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static float ReadFloat(ConfigNode node, string key, float fallback)
        {
            float value;
            return float.TryParse(node.GetValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static bool ReadBool(ConfigNode node, string key, bool fallback)
        {
            bool value;
            return bool.TryParse(node.GetValue(key), out value) ? value : fallback;
        }
    }
}
