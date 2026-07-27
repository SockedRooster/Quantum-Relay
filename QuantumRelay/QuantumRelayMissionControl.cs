using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Loads the save-specific Quantum Relay registry for non-flight scenes.
    ///
    /// Flight is the sole authority that publishes live network telemetry.
    /// Mission Control, the Space Center, and the Tracking Station are
    /// intentionally read-only so they cannot replace a multi-network snapshot
    /// with the old single "Primary Quantum Link" compatibility record.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    internal sealed class QuantumRelayMissionControl : MonoBehaviour
    {
        public void Start()
        {
            if (!IsMissionControlScene())
            {
                enabled = false;
                return;
            }

            Debug.Log(
                "[QuantumRelay] Mission Control starting in read-only mode: " +
                HighLogic.LoadedScene);

            QuantumRelaySettings.Load();
            QuantumRelayRegistry.EnsureLoaded();

            // Do not scan proto-vessels or call QuantumRelayRuntimeState.Publish().
            // Preserve the in-memory Flight snapshot during scene changes.
            // Scenario OnLoad restores it after a full save-game reload.
            enabled = false;
        }

        public void OnDestroy()
        {
            if (IsMissionControlScene())
            {
                Debug.Log(
                    "[QuantumRelay] Mission Control read-only loader destroyed: " +
                    HighLogic.LoadedScene);
            }
        }

        private static bool IsMissionControlScene()
        {
            return HighLogic.LoadedScene == GameScenes.SPACECENTER ||
                   HighLogic.LoadedScene == GameScenes.TRACKSTATION;
        }
    }
}
