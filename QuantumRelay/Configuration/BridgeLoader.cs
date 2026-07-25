using System.Collections.Generic;
using QuantumRelay.Core;
using UnityEngine;

namespace QuantumRelay.Configuration
{
    /// <summary>
    /// Loads quantum bridge definitions from KSP's GameDatabase.
    /// </summary>
    public static class BridgeLoader
    {
        public static List<QuantumBridge> Load()
        {
            List<QuantumBridge> bridges = new List<QuantumBridge>();

            ConfigNode[] nodes =
                GameDatabase.Instance.GetConfigNodes("QUANTUM_BRIDGE");

            foreach (ConfigNode node in nodes)
            {
                QuantumBridge bridge = new QuantumBridge();

                bridge.Name = node.GetValue("name");
                bridge.DisplayName = node.GetValue("displayName");
                bridge.GatewayA = node.GetValue("gatewayA");
                bridge.GatewayB = node.GetValue("gatewayB");

                string enabledText = node.GetValue("enabled");
                bool enabledValue;

                if (!string.IsNullOrEmpty(enabledText) &&
                    bool.TryParse(enabledText, out enabledValue))
                {
                    bridge.Enabled = enabledValue;
                }

                string activationRangeText =
                    node.GetValue("activationRange");

                double activationRangeValue;

                if (!string.IsNullOrEmpty(activationRangeText) &&
                    double.TryParse(
                        activationRangeText,
                        out activationRangeValue))
                {
                    bridge.ActivationRange = activationRangeValue;
                }

                string signalQualityText =
                    node.GetValue("signalQuality");

                float signalQualityValue;

                if (!string.IsNullOrEmpty(signalQualityText) &&
                    float.TryParse(
                        signalQualityText,
                        out signalQualityValue))
                {
                    bridge.SignalQuality = signalQualityValue;
                }

                string healthText = node.GetValue("health");
                float healthValue;

                if (!string.IsNullOrEmpty(healthText) &&
                    float.TryParse(healthText, out healthValue))
                {
                    bridge.Health = healthValue;
                }

                bridges.Add(bridge);
            }

            Debug.Log(
                "[QuantumRelay] Loaded " +
                bridges.Count +
                " bridge definition(s).");

            return bridges;
        }
    }
}