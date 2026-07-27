using QuantumRelay.Core;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Save-scoped owner for Quantum Relay runtime services. KSP creates one
    /// instance for each game and carries it across supported scenes.
    /// </summary>
    [KSPScenario(
        ScenarioCreationOptions.AddToAllGames,
        GameScenes.FLIGHT,
        GameScenes.SPACECENTER,
        GameScenes.TRACKSTATION)]
    public sealed class QuantumRelayScenario : ScenarioModule
    {
        private bool _awake;

        public override void OnAwake()
        {
            base.OnAwake();

            _awake = true;
            QuantumManager.Instance.Initialize("QuantumRelayScenario");

            Debug.Log(
                "[QuantumRelay] " +
                QuantumRelayConstants.DisplayVersion +
                " scenario initialized" +
                " | scene=" + HighLogic.LoadedScene);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            // Restore the last published network snapshot before any
            // non-flight GUI attempts to display it.
            QuantumRelayRegistry.LoadFromScenario(node);
            QuantumManager.Instance.Initialize("QuantumRelayScenario.OnLoad");

            Debug.Log(
                "[QuantumRelay] Scenario state loaded" +
                " | scene=" + HighLogic.LoadedScene +
                " | networks=" + QuantumRelayRegistry.Networks.Count);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            if (node != null)
            {
                node.SetValue(
                    "version",
                    QuantumRelayConstants.DisplayVersion,
                    true);
                node.SetValue(
                    "registeredRelayCount",
                    QuantumManager.Instance.RegisteredRelayCount.ToString(),
                    true);

                QuantumRelayRegistry.SaveToScenario(node);
                QuantumRelayRegistry.FlushToDisk("game save");
            }
        }

        public void OnDestroy()
        {
            if (!_awake)
                return;

            _awake = false;
            QuantumManager.Instance.Shutdown("scenario destroyed");
        }
    }
}
