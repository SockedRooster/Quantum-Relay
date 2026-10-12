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
        public static bool ShowFlightHud { get; private set; } = true;
        public static bool HudFollowStock { get; private set; } = true;
        public static float HudX { get; private set; } = 270f;
        public static float HudY { get; private set; } = 84f;
        public static float UiScale { get; private set; } = 1f;
        public static bool LastSaveSucceeded { get; private set; }
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
                ConfigNode node = QuantumRelaySettingsNode.Find(root);

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

                // Gateway distance is managed internally in RC2. Older saved
                // radius values are intentionally ignored for compatibility.
                GatewayRadiusMetres = DefaultGatewayRadiusMetres;
                AutoRebuildCommNet = ReadBool(node, "autoRebuildCommNet", true);
                ShowScreenMessages = ReadBool(node, "showScreenMessages", true);
                DebugLogging = ReadBool(node, "debugLogging", false);
                ShowFlightHud = ReadBool(node, "showFlightHud", true);
                HudFollowStock = ReadBool(node, "hudFollowStock", true);
                HudX = Math.Max(0f, ReadFloat(node, "hudX", 270f));
                HudY = Math.Max(0f, ReadFloat(node, "hudY", 84f));
                UiScale = Math.Max(0.75f, Math.Min(1.5f, ReadFloat(node, "uiScale", 1f)));
                WindowX = ReadFloat(node, "windowX", 260f);
                WindowY = ReadFloat(node, "windowY", 100f);
                WindowWidth = Math.Max(
                    460f,
                    ReadFloat(node, "windowWidth", 560f));
                WindowHeight = Math.Max(
                    420f,
                    ReadFloat(node, "windowHeight", 700f));

                Debug.Log("[QuantumRelay] Settings loaded.");
            }
            catch (Exception ex)
            {
                ResetDefaults(false);
                Debug.LogWarning("[QuantumRelay] Failed to load settings; defaults restored: " + ex.Message);
            }
        }

        public static bool Apply(bool autoRebuild,
            bool showMessages, bool debugLogging, bool save)
        {
            bool oldAuto = AutoRebuildCommNet;

            GatewayRadiusMetres = DefaultGatewayRadiusMetres;
            AutoRebuildCommNet = autoRebuild;
            ShowScreenMessages = showMessages;
            DebugLogging = debugLogging;

            if (save)
                Save();

            return oldAuto != AutoRebuildCommNet;
        }

        public static void ResetDefaults(bool save)
        {
            GatewayRadiusMetres = DefaultGatewayRadiusMetres;
            AutoRebuildCommNet = true;
            ShowScreenMessages = true;
            DebugLogging = false;
            ShowFlightHud = true;
            HudFollowStock = true;
            HudX = 270f;
            HudY = 84f;
            UiScale = 1f;
            WindowX = 260f;
            WindowY = 100f;
            WindowWidth = 560f;
            WindowHeight = 700f;
            if (save) Save();
        }

        public static void Save()
        {
            LastSaveSucceeded = false;
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                ConfigNode node = new ConfigNode("QUANTUM_RELAY_SETTINGS");
                node.AddValue("autoRebuildCommNet", AutoRebuildCommNet);
                node.AddValue("showScreenMessages", ShowScreenMessages);
                node.AddValue("debugLogging", DebugLogging);
                node.AddValue("showFlightHud", ShowFlightHud);
                node.AddValue("hudFollowStock", HudFollowStock);
                node.AddValue("hudX", HudX.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("hudY", HudY.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("uiScale", UiScale.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("windowX", WindowX.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("windowY", WindowY.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("windowWidth", WindowWidth.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("windowHeight", WindowHeight.ToString("0.##", CultureInfo.InvariantCulture));

                ConfigNode root = new ConfigNode("QUANTUM_RELAY_CONFIG");
                root.AddNode(node);
                if (!root.Save(SettingsPath)) throw new IOException("Settings file could not be written.");
                LastSaveSucceeded = true;
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

        public static void ApplyDisplay(bool showHud, bool followStock, float hudX, float hudY, float uiScale)
        {
            ShowFlightHud = showHud;
            HudFollowStock = followStock;
            HudX = Math.Max(0f, Finite(hudX, 270f));
            HudY = Math.Max(0f, Finite(hudY, 84f));
            UiScale = Math.Max(0.75f, Math.Min(1.5f, Finite(uiScale, 1f)));
        }

        private static float Finite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        public static void SaveWindowLayout(
            float x,
            float y,
            float width,
            float height)
        {
            WindowX = Math.Max(0f, Finite(x, 260f));
            WindowY = Math.Max(0f, Finite(y, 100f));
            WindowWidth = Math.Max(460f, Finite(width, 560f));
            WindowHeight = Math.Max(420f, Finite(height, 700f));
            Save();
        }




        private static float ReadFloat(ConfigNode node, string key, float fallback)
        {
            float value;
            return float.TryParse(node.GetValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? Finite(value, fallback) : fallback;
        }

        private static bool ReadBool(ConfigNode node, string key, bool fallback)
        {
            bool value;
            return bool.TryParse(node.GetValue(key), out value) ? value : fallback;
        }
    }
}
