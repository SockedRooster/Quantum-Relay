using System;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Public compatibility module for Quantum Relay-capable parts.
    /// Part authors can add this module in config without changing plugin code.
    /// </summary>
    public sealed class ModuleQuantumRelay : PartModule
    {
        [KSPField(isPersistant = true)]
        public bool relayEnabled = true;

        [KSPField]
        public bool requiresDeployment = true;

        [KSPField]
        public string deploymentModuleName = "ModuleDeployableReflector";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Quantum Relay")]
        public string relayStatus = "Standby";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            UpdateStatus();
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
                UpdateStatus();
        }

        public bool IsOperational()
        {
            if (!relayEnabled || part == null) return false;
            if (!requiresDeployment) return true;

            PartModule deployment = FindDeploymentModule();
            if (deployment == null) return false;

            ModuleDeployablePart deployable = deployment as ModuleDeployablePart;
            if (deployable != null)
                return deployable.deployState == ModuleDeployablePart.DeployState.EXTENDED;

            if (deployment.Fields != null)
            {
                foreach (string key in new[] { "deployState", "isDeployed", "deployed", "state", "status", "stateString" })
                {
                    BaseField field = deployment.Fields[key];
                    if (field == null) continue;
                    object value = field.GetValue(deployment);
                    if (LooksDeployed(value)) return true;
                }
            }
            return false;
        }

        private PartModule FindDeploymentModule()
        {
            if (part.Modules == null || string.IsNullOrEmpty(deploymentModuleName)) return null;
            foreach (PartModule module in part.Modules)
            {
                if (module != null && string.Equals(module.moduleName, deploymentModuleName, StringComparison.OrdinalIgnoreCase))
                    return module;
            }
            return null;
        }

        private void UpdateStatus()
        {
            relayStatus = !relayEnabled ? "Disabled" : (IsOperational() ? "Ready" : "Not Deployed");
        }

        private static bool LooksDeployed(object value)
        {
            if (value == null) return false;
            bool boolean;
            if (bool.TryParse(value.ToString(), out boolean)) return boolean;
            string text = value.ToString().Trim().ToUpperInvariant();
            return text.Contains("EXTENDED") || text.Contains("DEPLOYED");
        }
    }
}
