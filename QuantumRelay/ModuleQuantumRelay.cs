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
        private enum RelayDeploymentState
        {
            Fixed,
            Missing,
            Retracted,
            Extending,
            Extended,
            Retracting,
            Broken,
            Unknown
        }

        [KSPField(isPersistant = true)]
        public bool relayEnabled = true;

        [KSPField]
        public bool requiresDeployment = true;

        [KSPField]
        public string deploymentModuleName = "ModuleDeployableReflector";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Quantum Relay")]
        public string relayStatus = "Standby";

        [KSPEvent(
            guiActive = true,
            guiActiveEditor = false,
            guiName = "Enable Quantum Relay",
            active = true)]
        public void EnableRelay()
        {
            relayEnabled = true;
            UpdateStatus();
        }

        [KSPEvent(
            guiActive = true,
            guiActiveEditor = false,
            guiName = "Disable Quantum Relay",
            active = true)]
        public void DisableRelay()
        {
            relayEnabled = false;
            UpdateStatus();
        }

        [KSPEvent(
            guiActive = true,
            guiActiveEditor = false,
            guiName = "Extend Quantum Relay",
            active = true)]
        public void ExtendRelay()
        {
            InvokeDeploymentEvent(true);
            UpdateStatus();
        }

        [KSPEvent(
            guiActive = true,
            guiActiveEditor = false,
            guiName = "Retract Quantum Relay",
            active = true)]
        public void RetractRelay()
        {
            InvokeDeploymentEvent(false);
            UpdateStatus();
        }

        [KSPAction("Toggle Quantum Relay")]
        public void ToggleRelayAction(KSPActionParam parameter)
        {
            relayEnabled = !relayEnabled;
            UpdateStatus();
        }

        [KSPAction("Enable Quantum Relay")]
        public void EnableRelayAction(KSPActionParam parameter)
        {
            relayEnabled = true;
            UpdateStatus();
        }

        [KSPAction("Disable Quantum Relay")]
        public void DisableRelayAction(KSPActionParam parameter)
        {
            relayEnabled = false;
            UpdateStatus();
        }

        [KSPAction("Extend Quantum Relay")]
        public void ExtendRelayAction(KSPActionParam parameter)
        {
            InvokeDeploymentEvent(true);
            UpdateStatus();
        }

        [KSPAction("Retract Quantum Relay")]
        public void RetractRelayAction(KSPActionParam parameter)
        {
            InvokeDeploymentEvent(false);
            UpdateStatus();
        }

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

        /// <summary>
        /// Returns true when the relay is enabled and its deployment
        /// requirement has been satisfied.
        /// </summary>
        public bool IsOperational()
        {
            if (!relayEnabled || part == null)
                return false;

            if (!requiresDeployment)
                return true;

            return GetDeploymentState() == RelayDeploymentState.Extended;
        }

        private RelayDeploymentState GetDeploymentState()
        {
            if (!requiresDeployment)
                return RelayDeploymentState.Fixed;

            PartModule deployment = FindDeploymentModule();
            if (deployment == null)
                return RelayDeploymentState.Missing;

            ModuleDeployablePart deployable = deployment as ModuleDeployablePart;
            if (deployable != null)
            {
                switch (deployable.deployState)
                {
                    case ModuleDeployablePart.DeployState.RETRACTED:
                        return RelayDeploymentState.Retracted;

                    case ModuleDeployablePart.DeployState.EXTENDING:
                        return RelayDeploymentState.Extending;

                    case ModuleDeployablePart.DeployState.EXTENDED:
                        return RelayDeploymentState.Extended;

                    case ModuleDeployablePart.DeployState.RETRACTING:
                        return RelayDeploymentState.Retracting;

                    case ModuleDeployablePart.DeployState.BROKEN:
                        return RelayDeploymentState.Broken;

                    default:
                        return RelayDeploymentState.Unknown;
                }
            }

            if (deployment.Fields != null)
            {
                string[] fieldNames =
                {
                    "deployState",
                    "isDeployed",
                    "deployed",
                    "state",
                    "status",
                    "stateString"
                };

                foreach (string fieldName in fieldNames)
                {
                    BaseField field = deployment.Fields[fieldName];
                    if (field == null)
                        continue;

                    object value = field.GetValue(deployment);
                    RelayDeploymentState detectedState =
                        InterpretDeploymentValue(value);

                    if (detectedState != RelayDeploymentState.Unknown)
                        return detectedState;
                }
            }

            return RelayDeploymentState.Unknown;
        }

        private PartModule FindDeploymentModule()
        {
            if (part == null ||
                part.Modules == null ||
                string.IsNullOrEmpty(deploymentModuleName))
            {
                return null;
            }

            foreach (PartModule module in part.Modules)
            {
                if (module != null &&
                    string.Equals(
                        module.moduleName,
                        deploymentModuleName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return module;
                }
            }

            return null;
        }

        private void InvokeDeploymentEvent(bool extend)
        {
            if (!requiresDeployment)
                return;

            PartModule deployment = FindDeploymentModule();
            if (deployment == null || deployment.Events == null)
                return;

            string[] eventNames = extend
                ? new[]
                {
                    "Extend",
                    "Deploy",
                    "ExtendReflector",
                    "DeployReflector",
                    "ExtendAntenna"
                }
                : new[]
                {
                    "Retract",
                    "RetractReflector",
                    "RetractAntenna"
                };

            foreach (string eventName in eventNames)
            {
                BaseEvent deploymentEvent = deployment.Events[eventName];

                if (deploymentEvent == null)
                    continue;

                try
                {
                    deploymentEvent.Invoke();
                    return;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[QuantumRelay] Unable to invoke deployment event '" +
                        eventName +
                        "': " +
                        exception.Message);
                }
            }

            Debug.LogWarning(
                "[QuantumRelay] No compatible " +
                (extend ? "extend" : "retract") +
                " event was found on deployment module " +
                deployment.moduleName +
                ".");
        }

        private void UpdateStatus()
        {
            RelayDeploymentState deploymentState = GetDeploymentState();

            if (!relayEnabled)
            {
                relayStatus = "Disabled";
            }
            else
            {
                switch (deploymentState)
                {
                    case RelayDeploymentState.Fixed:
                        relayStatus = "Ready (Fixed)";
                        break;

                    case RelayDeploymentState.Extended:
                        relayStatus = "Ready (Extended)";
                        break;

                    case RelayDeploymentState.Extending:
                        relayStatus = "Deploying";
                        break;

                    case RelayDeploymentState.Retracting:
                        relayStatus = "Retracting";
                        break;

                    case RelayDeploymentState.Retracted:
                        relayStatus = "Retracted";
                        break;

                    case RelayDeploymentState.Broken:
                        relayStatus = "Deployment Hardware Broken";
                        break;

                    case RelayDeploymentState.Missing:
                        relayStatus = "Deployment Module Missing";
                        break;

                    default:
                        relayStatus = "Deployment State Unknown";
                        break;
                }
            }

            UpdateEventVisibility(deploymentState);
        }

        private void UpdateEventVisibility(
            RelayDeploymentState deploymentState)
        {
            BaseEvent enableEvent = Events["EnableRelay"];
            BaseEvent disableEvent = Events["DisableRelay"];
            BaseEvent extendEvent = Events["ExtendRelay"];
            BaseEvent retractEvent = Events["RetractRelay"];

            if (enableEvent != null)
                enableEvent.active = !relayEnabled;

            if (disableEvent != null)
                disableEvent.active = relayEnabled;

            bool canControlDeployment =
                requiresDeployment &&
                deploymentState != RelayDeploymentState.Missing &&
                deploymentState != RelayDeploymentState.Fixed &&
                deploymentState != RelayDeploymentState.Broken;

            if (extendEvent != null)
            {
                extendEvent.active =
                    canControlDeployment &&
                    (deploymentState == RelayDeploymentState.Retracted ||
                     deploymentState == RelayDeploymentState.Unknown);
            }

            if (retractEvent != null)
            {
                retractEvent.active =
                    canControlDeployment &&
                    deploymentState == RelayDeploymentState.Extended;
            }
        }

        private static RelayDeploymentState InterpretDeploymentValue(
            object value)
        {
            if (value == null)
                return RelayDeploymentState.Unknown;

            bool booleanValue;
            string text = value.ToString().Trim();

            if (bool.TryParse(text, out booleanValue))
            {
                return booleanValue
                    ? RelayDeploymentState.Extended
                    : RelayDeploymentState.Retracted;
            }

            string upper = text.ToUpperInvariant();

            if (upper.Contains("RETRACTING"))
                return RelayDeploymentState.Retracting;

            if (upper.Contains("EXTENDING") ||
                upper.Contains("DEPLOYING"))
            {
                return RelayDeploymentState.Extending;
            }

            if (upper.Contains("RETRACTED") ||
                upper.Contains("STOWED"))
            {
                return RelayDeploymentState.Retracted;
            }

            if (upper.Contains("EXTENDED") ||
                upper.Contains("DEPLOYED"))
            {
                return RelayDeploymentState.Extended;
            }

            if (upper.Contains("BROKEN") ||
                upper.Contains("FAILED"))
            {
                return RelayDeploymentState.Broken;
            }

            return RelayDeploymentState.Unknown;
        }
    }
}