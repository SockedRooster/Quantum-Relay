using System;
using UnityEngine;

namespace QuantumRelay
{
    internal sealed class RelayDeploymentController
    {
        private readonly Part part;
        private readonly bool requiresDeployment;
        private readonly string deploymentModuleName;
        private bool? commandedExtended;
        private float? previousAnimationTime;

        public RelayDeploymentController(
            Part part,
            bool requiresDeployment,
            string deploymentModuleName)
        {
            this.part = part;
            this.requiresDeployment = requiresDeployment;
            this.deploymentModuleName = deploymentModuleName;
        }

        public QuantumRelayDeploymentState GetState()
        {
            if (!requiresDeployment)
                return QuantumRelayDeploymentState.Fixed;

            PartModule deployment = FindModule();
            if (deployment == null)
                return QuantumRelayDeploymentState.Missing;

            // Rooster Works dishes use stock ModuleAnimateGeneric rather than
            // Near Future's ModuleDeployableReflector. Read its persisted
            // normalized animation progress without depending on a particular
            // KSP build exposing animTime as a public C# member.
            if (deployment is ModuleAnimateGeneric)
            {
                HideStockAnimationControls(deployment);
                float progress;
                if (!TryReadAnimationTime(deployment, out progress))
                    return QuantumRelayDeploymentState.Unknown;
                if (progress >= 0.995f)
                {
                    previousAnimationTime = progress;
                    commandedExtended = true;
                    return QuantumRelayDeploymentState.Extended;
                }
                if (progress <= 0.005f)
                {
                    previousAnimationTime = progress;
                    commandedExtended = false;
                    return QuantumRelayDeploymentState.Retracted;
                }
                // Actual motion wins over the last button command, including
                // stock action-group toggles and animation reversal.
                if (previousAnimationTime.HasValue)
                {
                    float delta = progress - previousAnimationTime.Value;
                    if (delta > 0.0001f) commandedExtended = true;
                    else if (delta < -0.0001f) commandedExtended = false;
                }
                previousAnimationTime = progress;
                return commandedExtended == false
                    ? QuantumRelayDeploymentState.Retracting
                    : QuantumRelayDeploymentState.Extending;
            }

            ModuleDeployablePart deployable =
                deployment as ModuleDeployablePart;

            if (deployable != null)
            {
                switch (deployable.deployState)
                {
                    case ModuleDeployablePart.DeployState.RETRACTED:
                        return QuantumRelayDeploymentState.Retracted;
                    case ModuleDeployablePart.DeployState.EXTENDING:
                        return QuantumRelayDeploymentState.Extending;
                    case ModuleDeployablePart.DeployState.EXTENDED:
                        return QuantumRelayDeploymentState.Extended;
                    case ModuleDeployablePart.DeployState.RETRACTING:
                        return QuantumRelayDeploymentState.Retracting;
                    case ModuleDeployablePart.DeployState.BROKEN:
                        return QuantumRelayDeploymentState.Broken;
                    default:
                        return QuantumRelayDeploymentState.Unknown;
                }
            }

            string[] fieldNames =
            {
                "deployState",
                "isDeployed",
                "deployed",
                "state",
                "status",
                "stateString"
            };

            if (deployment.Fields != null)
            {
                foreach (string fieldName in fieldNames)
                {
                    BaseField field = deployment.Fields[fieldName];
                    if (field == null)
                        continue;

                    QuantumRelayDeploymentState interpreted =
                        InterpretValue(field.GetValue(deployment));

                    if (interpreted != QuantumRelayDeploymentState.Unknown)
                        return interpreted;
                }
            }

            return QuantumRelayDeploymentState.Unknown;
        }

        public bool Invoke(bool extend)
        {
            if (!requiresDeployment)
                return false;

            PartModule deployment = FindModule();
            if (deployment == null || deployment.Events == null)
                return false;

            if (deployment is ModuleAnimateGeneric)
            {
                QuantumRelayDeploymentState current = GetState();
                if ((extend && current == QuantumRelayDeploymentState.Extended) ||
                    (!extend && current == QuantumRelayDeploymentState.Retracted))
                    return true;

                // Toggle is the stock ModuleAnimateGeneric event; unlike the
                // old reflector, there are no separate Extend/Retract events.
                BaseEvent toggle = deployment.Events["Toggle"];
                if (toggle == null)
                {
                    Debug.LogWarning("[QuantumRelay] ModuleAnimateGeneric Toggle event missing.");
                    return false;
                }
                try
                {
                    toggle.Invoke();
                    commandedExtended = extend;
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[QuantumRelay] Animation toggle failed: " + ex.Message);
                    return false;
                }
            }

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
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[QuantumRelay] Deployment event '" +
                        eventName +
                        "' failed: " +
                        exception.Message);
                }
            }

            Debug.LogWarning(
                "[QuantumRelay] No compatible " +
                (extend ? "extend" : "retract") +
                " event found on " +
                deployment.moduleName +
                ".");

            return false;
        }

        private static void HideStockAnimationControls(PartModule module)
        {
            if (module == null || module.Events == null) return;
            // Keep Toggle callable programmatically; only hide the duplicate PAW button.
            // ModuleAnimateGeneric normally exposes Toggle, but some part setups
            // also expose Deploy/Retract event names. Hide all stock PAW events.
            // Do not disable the events: our custom controls still invoke Toggle.
            string[] stockEvents = { "Toggle", "Deploy", "Retract", "Extend" };
            foreach (string eventName in stockEvents)
            {
                BaseEvent stockEvent = module.Events[eventName];
                if (stockEvent == null) continue;
                stockEvent.guiActive = false;
                stockEvent.guiActiveEditor = false;
            }
        }

        private static bool TryReadAnimationTime(PartModule module, out float progress)
        {
            progress = 0f;
            try
            {
                object value = null;
                if (module.Fields != null)
                {
                    BaseField field = module.Fields["animTime"];
                    if (field != null) value = field.GetValue(module);
                }
                if (value == null)
                {
                    const System.Reflection.BindingFlags flags =
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;
                    var type = module.GetType();
                    var field = type.GetField("animTime", flags);
                    if (field != null) value = field.GetValue(module);
                    else
                    {
                        var property = type.GetProperty("animTime", flags);
                        if (property != null) value = property.GetValue(module, null);
                    }
                }
                if (value == null) return false;
                progress = Convert.ToSingle(value,
                    System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QuantumRelay] Animation progress read failed: " + ex.Message);
                return false;
            }
        }

        private PartModule FindModule()
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

        private static QuantumRelayDeploymentState InterpretValue(
            object value)
        {
            if (value == null)
                return QuantumRelayDeploymentState.Unknown;

            bool booleanValue;
            string text = value.ToString().Trim();

            if (bool.TryParse(text, out booleanValue))
            {
                return booleanValue
                    ? QuantumRelayDeploymentState.Extended
                    : QuantumRelayDeploymentState.Retracted;
            }

            string upper = text.ToUpperInvariant();

            if (upper.Contains("RETRACTING"))
                return QuantumRelayDeploymentState.Retracting;
            if (upper.Contains("EXTENDING") ||
                upper.Contains("DEPLOYING"))
                return QuantumRelayDeploymentState.Extending;
            if (upper.Contains("RETRACTED") ||
                upper.Contains("STOWED"))
                return QuantumRelayDeploymentState.Retracted;
            if (upper.Contains("EXTENDED") ||
                upper.Contains("DEPLOYED"))
                return QuantumRelayDeploymentState.Extended;
            if (upper.Contains("BROKEN") ||
                upper.Contains("FAILED"))
                return QuantumRelayDeploymentState.Broken;

            return QuantumRelayDeploymentState.Unknown;
        }

        public static string GetDisplayName(
            QuantumRelayDeploymentState state)
        {
            switch (state)
            {
                case QuantumRelayDeploymentState.Fixed:
                    return "Fixed";
                case QuantumRelayDeploymentState.Missing:
                    return "Module Missing";
                case QuantumRelayDeploymentState.Retracted:
                    return "Retracted";
                case QuantumRelayDeploymentState.Extending:
                    return "Extending";
                case QuantumRelayDeploymentState.Extended:
                    return "Extended";
                case QuantumRelayDeploymentState.Retracting:
                    return "Retracting";
                case QuantumRelayDeploymentState.Broken:
                    return "Broken";
                default:
                    return "Unknown";
            }
        }
    }
}
