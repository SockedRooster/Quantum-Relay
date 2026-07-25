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
        public const int DefaultSignalQualityPercent = QuantumRelayConstants.DefaultSignalQualityPercent;
        public const double DefaultGatewayRadiusMetres = QuantumRelayConstants.DefaultGatewayRadiusMetres;
        public const double DefaultElectricChargePerSecondPerGateway = QuantumRelayConstants.DefaultElectricChargePerSecondPerGateway;

        public static int SignalQualityPercent { get; private set; } = DefaultSignalQualityPercent;
        public static double GatewayRadiusMetres { get; private set; } = DefaultGatewayRadiusMetres;
        public static double ElectricChargePerSecondPerGateway { get; private set; } = DefaultElectricChargePerSecondPerGateway;
        public static bool AutoRebuildCommNet { get; private set; } = true;
        public static bool ShowScreenMessages { get; private set; } = true;
        public static bool DebugLogging { get; private set; }
        public static float WindowX { get; private set; } = 260f;
        public static float WindowY { get; private set; } = 100f;

        public static double SignalQualityMultiplier => SignalQualityPercent / 100.0;

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
                    Debug.LogWarning("[QuantumRelay] Settings node missing; defaults restored.");
                    Save();
                    return;
                }

                SignalQualityPercent = SanitizeSignal(ReadInt(node, "signalQualityPercent", DefaultSignalQualityPercent));
                GatewayRadiusMetres = SanitizeRadius(ReadDouble(node, "gatewayRadiusMetres", DefaultGatewayRadiusMetres));
                ElectricChargePerSecondPerGateway = SanitizePower(ReadDouble(node, "electricChargePerSecondPerGateway", DefaultElectricChargePerSecondPerGateway));
                AutoRebuildCommNet = ReadBool(node, "autoRebuildCommNet", true);
                ShowScreenMessages = ReadBool(node, "showScreenMessages", true);
                DebugLogging = ReadBool(node, "debugLogging", false);
                WindowX = ReadFloat(node, "windowX", 260f);
                WindowY = ReadFloat(node, "windowY", 100f);

                Debug.Log("[QuantumRelay] Settings loaded | signal=" + SignalQualityPercent +
                          "% | radius=" + GatewayRadiusMetres.ToString("0", CultureInfo.InvariantCulture) +
                          "m | power=" + ElectricChargePerSecondPerGateway.ToString("0.##", CultureInfo.InvariantCulture) + " EC/s");
            }
            catch (Exception ex)
            {
                ResetDefaults(false);
                Debug.LogWarning("[QuantumRelay] Failed to load settings; defaults restored: " + ex.Message);
            }
        }

        public static bool Apply(int signalQualityPercent, double radiusMetres, double powerPerGateway,
            bool autoRebuild, bool showMessages, bool debugLogging, bool save)
        {
            int oldSignal = SignalQualityPercent;
            double oldRadius = GatewayRadiusMetres;
            double oldPower = ElectricChargePerSecondPerGateway;
            bool oldAuto = AutoRebuildCommNet;

            SignalQualityPercent = SanitizeSignal(signalQualityPercent);
            GatewayRadiusMetres = SanitizeRadius(radiusMetres);
            ElectricChargePerSecondPerGateway = SanitizePower(powerPerGateway);
            AutoRebuildCommNet = autoRebuild;
            ShowScreenMessages = showMessages;
            DebugLogging = debugLogging;

            if (save)
                Save();

            return oldSignal != SignalQualityPercent ||
                   Math.Abs(oldRadius - GatewayRadiusMetres) > 0.01 ||
                   Math.Abs(oldPower - ElectricChargePerSecondPerGateway) > 0.001 ||
                   oldAuto != AutoRebuildCommNet;
        }

        public static void ResetDefaults(bool save)
        {
            SignalQualityPercent = DefaultSignalQualityPercent;
            GatewayRadiusMetres = DefaultGatewayRadiusMetres;
            ElectricChargePerSecondPerGateway = DefaultElectricChargePerSecondPerGateway;
            AutoRebuildCommNet = true;
            ShowScreenMessages = true;
            DebugLogging = false;
            WindowX = 260f;
            WindowY = 100f;
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
                node.AddValue("signalQualityPercent", SignalQualityPercent);
                node.AddValue("gatewayRadiusMetres", GatewayRadiusMetres.ToString("0", CultureInfo.InvariantCulture));
                node.AddValue("electricChargePerSecondPerGateway", ElectricChargePerSecondPerGateway.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("autoRebuildCommNet", AutoRebuildCommNet);
                node.AddValue("showScreenMessages", ShowScreenMessages);
                node.AddValue("debugLogging", DebugLogging);
                node.AddValue("windowX", WindowX.ToString("0.##", CultureInfo.InvariantCulture));
                node.AddValue("windowY", WindowY.ToString("0.##", CultureInfo.InvariantCulture));
                node.Save(SettingsPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QuantumRelay] Failed to save settings: " + ex.Message);
            }
        }


        public static void SaveWindowPosition(float x, float y)
        {
            WindowX = Math.Max(0f, x);
            WindowY = Math.Max(0f, y);
            Save();
        }

        private static int SanitizeSignal(int value)
        {
            value = Math.Max(10, Math.Min(100, value));
            return (int)Math.Round(value / 10.0, MidpointRounding.AwayFromZero) * 10;
        }

        private static double SanitizeRadius(double value)
        {
            value = Math.Max(100000.0, Math.Min(500000.0, value));
            return Math.Round(value / 25000.0, MidpointRounding.AwayFromZero) * 25000.0;
        }

        private static double SanitizePower(double value)
        {
            return Math.Max(0.0, Math.Min(50.0, Math.Round(value, 1)));
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
