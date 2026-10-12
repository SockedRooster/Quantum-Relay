using System;
using System.Collections.Generic;
using QuantumRelay.Core;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// KSP-facing module for Quantum Relay hardware.
    /// Coordinates deployment, power, synchronization, diagnostics, and PAW UI.
    /// </summary>
    public sealed class ModuleQuantumRelay : PartModule
    {
        private RelayStateMachine stateMachine;
        private RelayDeploymentController deploymentController;
        private RelayPowerController powerController;
        private RelaySynchronizationController synchronizationController;
        private RelayDefinition relayDefinition;
        private QuantumRelayVisualController visualController;
        private bool effectsInitialized;
        private const double StatusLightPowerRate = 0.5;
        private bool statusLightPowered;
        private bool transmitterWasBusy;
        private double lastBatteryFraction = 1.0;
        private bool? lastDeploymentExtended;

        private QuantumRelayDeploymentState deploymentState =
            QuantumRelayDeploymentState.Unknown;

        private QuantumRelayOperationalState operationalState =
            QuantumRelayOperationalState.Unknown;

        private bool hasCommNetHardware;
        private double nextStatusRefreshTime;
        private double hardwareNotReadySince = -1.0;

        // Existing persistent field retained for save/config compatibility.
        [KSPField(isPersistant = true)]
        public bool relayEnabled = true;

        [KSPField(
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Relay Model")]
        public string relayModelStatus = "Quantum Relay";

        [KSPField(
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Quantum Relay")]
        public string relayStatus = "Standby";

        // Editor-only diagnostics: selection and actual converter state are
        // shown independently so Kerbalism planner issues can be isolated.
        [KSPField(guiActive = false, guiActiveEditor = true,
            guiName = "Power Preview")]
        public string editorPowerPreviewStatus = "Off";

        [KSPField(guiActive = false, guiActiveEditor = true,
            guiName = "Power Backend")]
        public string editorPowerModuleInventory = "Not inspected";

        [KSPField(guiActive = false, guiActiveEditor = true,
            guiName = "Selected EC Rate")]
        public string editorSelectedEcRate = "0.00 EC/s";

        // Optional Kerbalism reflection hook. It only reports editor estimates;
        // no ProcessController, persisted preview state, or flight resource request.
        public string PlannerUpdate(List<KeyValuePair<string, double>> resources,
            CelestialBody body, Dictionary<string, double> environment)
        {
            if (!HighLogic.LoadedSceneIsEditor || powerController == null)
                return "Quantum Relay";
            double rate = 0.0;
            switch (powerController.EditorPreviewMode)
            {
                case 0: rate = idlePowerRate; break;
                case 1: rate = synchronizationPowerRate; break;
                case 2: rate = operationalPowerRate; break;
            }
            if (rate > 0.0)
                resources.Add(new KeyValuePair<string, double>("ElectricCharge", -rate));
            return "Quantum Relay " + powerController.PreviewModeName;
        }

        private void RefreshEditorPowerPreview()
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;

            editorPowerPreviewStatus = powerController != null
                ? powerController.EditorPreviewStatus : "Controller unavailable";
            editorPowerModuleInventory = powerController != null
                ? powerController.GetModuleInventory(part) : "Unavailable";
            double selectedRate = 0.0;
            if (powerController != null)
            {
                switch (powerController.EditorPreviewMode)
                {
                    case 0: selectedRate = idlePowerRate; break;
                    case 1: selectedRate = synchronizationPowerRate; break;
                    case 2: selectedRate = operationalPowerRate; break;
                }
            }
            editorSelectedEcRate = selectedRate.ToString("0.00") + " EC/s";
        }

        [KSPField(guiActive = true, guiName = "Operational")]
        public string operationalStatus = "NO";

        [KSPField(guiActive = true, guiName = "CommNet")]
        public string commNetStatus = "Not Detected";

        [KSPField(guiActive = true, guiName = "Battery Protection")]
        public string batteryProtectionStatus = "Ready";

        [KSPField(isPersistant = true, guiActive = true, guiName = "Automatic Power Protection")]
        [UI_Toggle(enabledText = "ON", disabledText = "OFF")]
        public bool automaticPowerProtection = true;

        [KSPField(guiActive = true, guiName = "Deployment")]
        public string deploymentStatus = "Unknown";

        [KSPField(guiActive = true, guiName = "Synchronization")]
        public string synchronizationStatus = "Not Synchronized";

        [KSPField(guiActive = true, guiName = "Power Usage")]
        public string powerUsageStatus = "0.00 EC/s";

        [KSPField(guiActive = false, guiName = "Status")]
        public string connectionStatus = "Standby";

        [KSPField(guiActive = true, guiName = "Connected Partner")]
        public string connectedPartnerStatus = "No Partner Connected";

        [KSPField(guiActive = true, guiName = "Quantum Signal")]
        public string quantumSignalStatus = "Unavailable";

        [KSPField(guiActive = false, guiName = "Electric Charge")]
        public string powerStatus = "Unavailable";



        [KSPField(isPersistant = true)]
        public bool batteryProtectionLatched;

        [KSPField]
        public double batteryShutdownFraction = 0.05;

        [KSPField]
        public double batteryRestartFraction = 0.25;


        [KSPField(isPersistant = true)]
        public double synchronizationProgress;

        [KSPField(isPersistant = true)]
        public bool relaySynchronized;

        [KSPField(isPersistant = true)]
        public string persistedOperationalState = "Unknown";

        [KSPField]
        public bool requiresDeployment = true;

        // Existing default retained for current reflector-based test hardware.
        [KSPField]
        public string deploymentModuleName = "ModuleDeployableReflector";

        [KSPField]
        public string relayClass = "Pioneer";

        [KSPField]
        public string relayModel = "Quantum Relay";

        [KSPField]
        public bool requiresCommNetHardware = true;

        [KSPField]
        public double idlePowerRate = 0.02;

        [KSPField]
        public double synchronizationPowerRate = 1.0;

        [KSPField]
        public double operationalPowerRate = 0.5;

        [KSPField]
        public double synchronizationDuration = 10.0;


        [KSPField]
        public double deploymentLossGracePeriod = 2.0;

        // Presentation-only settings. These must never gate deployment, power,
        // synchronization, registration, or routing.
        [KSPField]
        public bool enableEffects = true;

        [KSPField]
        public string activationSound = "QuantumRelay/Sounds/Activation";

        [KSPField]
        public string transmissionSound = "QuantumRelay/Sounds/TransmissionPulse";

        [KSPField]
        public float effectsVolume = 0.65f;

        [KSPField]
        public float statusLightRange = 12.0f;

        [KSPField]
        public float statusLightIntensity = 1.25f;

        [KSPField]
        public float statusLightOffsetX = 0.0f;

        [KSPField]
        public float statusLightOffsetY = 0.0f;

        [KSPField]
        public float statusLightOffsetZ = 0.0f;

        // RC4 energy-ring tuning. Offsets are in the part model's local space.
        [KSPField]
        public float energyRingRadius = 1.25f;

        [KSPField]
        public float energyRingWidth = 0.055f;

        [KSPField]
        public int energyRingSegments = 24;

        [KSPField]
        public float energyRingTiltX = 0.0f;

        [KSPField]
        public float energyRingTiltY = 0.0f;

        [KSPField]
        public float energyRingTiltZ = 0.0f;

        // RC4 per-panel emission. The visual system automatically finds the
        // repeated flat reflector-panel renderers on the deployed part.
        [KSPField]
        public bool enablePerHexEmission = true;

        [KSPField]
        public float perHexGlowRadius = 0.18f;

        [KSPField]
        public float perHexGlowWidth = 0.025f;

        [KSPField]
        public int perHexMaximumEmitters = 48;

        [KSPField]
        public bool perHexConvergenceLines = true;

        [KSPField]
        public string startupStage1 = "";

        [KSPField]
        public string startupStage2 = "";

        [KSPField]
        public string startupStage3 = "";

        [KSPField]
        public string startupStage4 = "";

        [KSPField]
        public string startupComplete = "";

        [KSPField]
        public double statusRefreshInterval = 0.25;

        [KSPField]
        public bool resetSynchronizationWhenRetracted = true;

        [KSPField]
        public bool resetSynchronizationWhenDisabled = true;










        public QuantumRelayDeploymentState DeploymentState
        {
            get { return deploymentState; }
        }

        public QuantumRelayOperationalState OperationalState
        {
            get { return operationalState; }
        }

        public bool IsSynchronized
        {
            get
            {
                return synchronizationController != null
                    ? synchronizationController.IsSynchronized
                    : relaySynchronized;
            }
        }

        public double SynchronizationFraction
        {
            get
            {
                return synchronizationController != null
                    ? synchronizationController.Progress
                    : synchronizationProgress;
            }
        }

        public double CurrentPowerRate
        {
            get
            {
                return powerController != null
                    ? powerController.CurrentRate
                    : 0.0;
            }
        }

        public bool HasCommNetHardware
        {
            get { return hasCommNetHardware; }
        }

        public RelayClass RelayClass
        {
            get
            {
                return relayDefinition != null
                    ? relayDefinition.RelayClass
                    : RelayCatalog.Parse(relayClass, 1);
            }
        }

        public int RelayTier
        {
            get
            {
                return relayDefinition != null
                    ? relayDefinition.Tier
                    : RelayCatalog.Get(RelayClass).Tier;
            }
        }

        public int DesignCapacity
        {
            get
            {
                return relayDefinition != null
                    ? relayDefinition.DesignCapacity
                    : RelayCatalog.Get(RelayClass).DesignCapacity;
            }
        }

        public double SignalStrengthMultiplier
        {
            get
            {
                return relayDefinition != null
                    ? relayDefinition.SynchronizationStrength
                    : RelayCatalog.Get(RelayClass).SynchronizationStrength;
            }
        }

        public string OperationalStateName
        {
            get { return operationalState.ToString(); }
        }

        // Explicit editor events: stock ModuleResourceConverter controls may be
        // hidden by converter/Kerbalism UI refresh after module startup.
        // These operate only in the editor; flight power is state-controlled.
        [KSPEvent(guiActive = false, guiActiveEditor = true,
            guiName = "Start Standby Power", active = true)]
        public void PreviewStandbyPower()
        {
            if (HighLogic.LoadedSceneIsEditor && powerController != null)
                {
                powerController.SetEditorPreview(0);
                RefreshEditorPowerPreview();
            }
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true,
            guiName = "Start Synchronization Power", active = true)]
        public void PreviewSynchronizationPower()
        {
            if (HighLogic.LoadedSceneIsEditor && powerController != null)
                {
                powerController.SetEditorPreview(1);
                RefreshEditorPowerPreview();
            }
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true,
            guiName = "Start Operational Power", active = true)]
        public void PreviewOperationalPower()
        {
            if (HighLogic.LoadedSceneIsEditor && powerController != null)
                {
                powerController.SetEditorPreview(2);
                RefreshEditorPowerPreview();
            }
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true,
            guiName = "Stop Power Preview", active = true)]
        public void StopPowerPreview()
        {
            if (HighLogic.LoadedSceneIsEditor && powerController != null)
                {
                powerController.SetEditorPreview(-1);
                RefreshEditorPowerPreview();
            }
        }

        [KSPEvent(
            guiActive = true,
            guiActiveEditor = false,
            guiName = "Enable Quantum Relay",
            active = true)]
        public void EnableRelay()
        {
            relayEnabled = true;
            ForceRefresh();
        }

        [KSPEvent(
            guiActive = true,
            guiActiveEditor = false,
            guiName = "Disable Quantum Relay",
            active = true)]
        public void DisableRelay()
        {
            relayEnabled = false;

            if (resetSynchronizationWhenDisabled)
                ResetSynchronization();

            ForceRefresh();
        }

        [KSPEvent(
            guiActive = true,
            guiActiveEditor = false,
            guiName = "Extend Quantum Relay",
            active = true)]
        public void ExtendRelay()
        {
            if (deploymentController != null)
            {
                lastDeploymentExtended = true;
                deploymentController.Invoke(true);
            }

            ForceRefresh();
        }

        [KSPEvent(
            guiActive = true,
            guiActiveEditor = false,
            guiName = "Retract Quantum Relay",
            active = true)]
        public void RetractRelay()
        {
            if (deploymentController != null)
            {
                lastDeploymentExtended = false;
                deploymentController.Invoke(false);
            }

            ForceRefresh();
        }

        [KSPEvent(
            guiActive = true,
            guiActiveEditor = false,
            guiName = "Reset Synchronization",
            active = true)]
        public void ResetSynchronizationEvent()
        {
            ResetSynchronization();
            ForceRefresh();
        }

        [KSPAction("Toggle Quantum Relay")]
        public void ToggleRelayAction(KSPActionParam parameter)
        {
            relayEnabled = !relayEnabled;

            if (!relayEnabled && resetSynchronizationWhenDisabled)
                ResetSynchronization();

            ForceRefresh();
        }

        [KSPAction("Enable Quantum Relay")]
        public void EnableRelayAction(KSPActionParam parameter)
        {
            EnableRelay();
        }

        [KSPAction("Disable Quantum Relay")]
        public void DisableRelayAction(KSPActionParam parameter)
        {
            DisableRelay();
        }

        [KSPAction("Extend Quantum Relay")]
        public void ExtendRelayAction(KSPActionParam parameter)
        {
            ExtendRelay();
        }

        [KSPAction("Retract Quantum Relay")]
        public void RetractRelayAction(KSPActionParam parameter)
        {
            RetractRelay();
        }

        [KSPAction("Toggle Relay Deployment")]
        public void ToggleDeploymentAction(KSPActionParam parameter)
        {
            if (deploymentController == null)
                return;

            deploymentState = deploymentController.GetState();

            if (deploymentState == QuantumRelayDeploymentState.Retracted ||
                deploymentState == QuantumRelayDeploymentState.Retracting)
            {
                lastDeploymentExtended = true;
                deploymentController.Invoke(true);
            }
            else if (
                deploymentState == QuantumRelayDeploymentState.Extended ||
                deploymentState == QuantumRelayDeploymentState.Extending)
            {
                lastDeploymentExtended = false;
                deploymentController.Invoke(false);
            }

            ForceRefresh();
        }

        [KSPAction("Reset Relay Synchronization")]
        public void ResetSynchronizationAction(KSPActionParam parameter)
        {
            ResetSynchronization();
            ForceRefresh();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            ApplyOfficialRelayDefinition();

            stateMachine = new RelayStateMachine();
            deploymentController = new RelayDeploymentController(
                part,
                requiresDeployment,
                deploymentModuleName);
            powerController = new RelayPowerController();
            powerController.Configure(part);
            synchronizationController =
                new RelaySynchronizationController();

            synchronizationController.Restore(
                synchronizationProgress,
                relaySynchronized);

            if (string.IsNullOrEmpty(relayModel))
            {
                relayModel =
                    part != null && part.partInfo != null
                        ? part.partInfo.title
                        : "Quantum Relay";
            }

            RefreshDiagnostics();
            UpdateBatteryProtection();
            EvaluateState(!batteryProtectionLatched);

            // Effects are initialized after the part and its deployable modules
            // have completed startup. Keeping this work out of OnStart and
            // FixedUpdate preserves the stable RC2 synchronization path.
            EnsureEffectsInitialized();
            UpdateEffectsState();
            CheckForTransmissionPulse();

            UpdateDisplayFields();
            RefreshEditorPowerPreview();
            UpdateEventVisibility();

            if (HighLogic.LoadedSceneIsFlight)
                QuantumManager.Instance.RegisterRelay(this);
        }

        public void OnDestroy()
        {
            QuantumManager.Instance.UnregisterRelay(this);
        }

        public void LateUpdate()
        {
            // Reassert after stock/mod Update methods rebuild PAW visibility.
            if (powerController != null)
                powerController.RefreshPowerInterface();
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (powerController != null && Time.time >= nextStatusRefreshTime)
                {
                    nextStatusRefreshTime = Time.time + 0.5f;
                    powerController.ApplyEditorPreview();
                    RefreshEditorPowerPreview();
                }
                return;
            }

            if (!HighLogic.LoadedSceneIsFlight)
                return;

            if (Time.time < nextStatusRefreshTime)
                return;

            nextStatusRefreshTime =
                Time.time + Math.Max(0.05, statusRefreshInterval);

            RefreshDiagnostics();
            UpdateBatteryProtection();
            EvaluateState(!batteryProtectionLatched);
            EnsureEffectsInitialized();
            UpdateEffectsState();
            CheckForTransmissionPulse();
            UpdateDisplayFields();
            UpdateEventVisibility();
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight ||
                part == null ||
                part.vessel == null ||
                stateMachine == null ||
                deploymentController == null ||
                powerController == null ||
                synchronizationController == null)
            {
                return;
            }

            deploymentState = deploymentController.GetState();
            ApplyDeploymentDirection();
            UpdateBatteryProtection();
            if (IsKerbalismRelayBroken())
            {
                deploymentState = QuantumRelayDeploymentState.Broken;
                ResetSynchronization();
            }
            RefreshCommNetStatus();

            bool hardwareReady =
                deploymentState == QuantumRelayDeploymentState.Fixed ||
                deploymentState == QuantumRelayDeploymentState.Extended;

            // Some third-party deployable reflector modules briefly report an
            // indeterminate or retracted state while updating their animation.
            // Debounce that transient state so a healthy relay does not drop and
            // restart synchronization every few seconds.
            if (hardwareReady)
            {
                hardwareNotReadySince = -1.0;
            }
            else if (resetSynchronizationWhenRetracted)
            {
                if (hardwareNotReadySince < 0.0)
                    hardwareNotReadySince = Time.time;

                double grace = Math.Max(0.0, deploymentLossGracePeriod);
                if (Time.time - hardwareNotReadySince >= grace)
                    ResetSynchronization();
            }

            QuantumRelayOperationalState prePowerState =
                stateMachine.Evaluate(
                    relayEnabled,
                    deploymentState,
                    requiresCommNetHardware,
                    hasCommNetHardware,
                    true,
                    synchronizationController.IsSynchronized);

            // Keep the gateway's underlying operational state independent of
            // routing telemetry: routing itself uses that state to discover links.
            // Only the converter selection and PAW use the actual link status.
            bool linked = HasActiveQuantumLink();
            QuantumRelayOperationalState powerMode = prePowerState;
            if (batteryProtectionLatched)
                powerMode = QuantumRelayOperationalState.Retracted;
            else if (prePowerState == QuantumRelayOperationalState.Operational && !linked)
                powerMode = QuantumRelayOperationalState.Synchronizing;

            double requiredRate = powerController.GetRequiredRate(
                powerMode,
                relayEnabled,
                idlePowerRate,
                synchronizationPowerRate,
                operationalPowerRate);

            bool hasPower = powerController.Consume(
                part,
                powerMode,
                requiredRate,
                Math.Max(0.0, TimeWarp.fixedDeltaTime));

            bool canSynchronize =
                relayEnabled &&
                !batteryProtectionLatched &&
                hardwareReady &&
                hasPower &&
                (!requiresCommNetHardware || hasCommNetHardware);

            if (canSynchronize &&
                !synchronizationController.IsSynchronized)
            {
                synchronizationController.Tick(
                    synchronizationDuration,
                    TimeWarp.fixedDeltaTime);
            }

            PersistSynchronizationState();
            // Dedicated status indicator consumer, in addition to relay converters.
            // Never drain the protected battery while suspended or manually disabled.
            bool lightWanted = enableEffects && relayEnabled && !batteryProtectionLatched &&
                hardwareReady && hasPower && !IsKerbalismRelayBroken();
            statusLightPowered = false;
            if (lightWanted)
            {
                double requested = StatusLightPowerRate * Math.Max(0.0, TimeWarp.fixedDeltaTime);
                if (requested > 0.0)
                    statusLightPowered = part.RequestResource("ElectricCharge", requested) >= requested * 0.999;
            }
            EvaluateState(hasPower && !batteryProtectionLatched);
            UpdateEffectsState();
        }

        // Link telemetry is presentation/power-mode input only; never use it to
        // determine IsOperational(), otherwise gateway discovery deadlocks.
        private bool HasActiveQuantumLink()
        {
            if (part == null || part.vessel == null) return false;
            foreach (ActiveQuantumLink link in QuantumRelayRuntimeState.Links)
            {
                if (link == null || !link.Online || !link.HasValidGateways) continue;
                if (link.GatewayA.Vessel.id == part.vessel.id ||
                    link.GatewayB.Vessel.id == part.vessel.id) return true;
            }
            return false;
        }

        private void UpdateBatteryProtection()
        {
            if (!HighLogic.LoadedSceneIsFlight || part == null || part.vessel == null)
                return;
            double available, capacity;
            try
            {
                part.vessel.GetConnectedResourceTotals(
                    PartResourceLibrary.ElectricityHashcode, out available, out capacity);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[QuantumRelay] Battery protection resource check: " + exception.Message);
                return;
            }
            if (capacity <= 0.0001) return;
            lastBatteryFraction = Math.Max(0.0, Math.Min(1.0, available / capacity));
            double low = Math.Max(0.0, Math.Min(1.0, batteryShutdownFraction));
            double high = Math.Max(low + 0.001, Math.Min(1.0, batteryRestartFraction));
            if (!automaticPowerProtection)
            {
                batteryProtectionLatched = false;
                batteryProtectionStatus = "OFF";
                return;
            }
            if (!batteryProtectionLatched && lastBatteryFraction <= low)
            {
                batteryProtectionLatched = true;
                ResetSynchronization();
                if (powerController != null) powerController.Shutdown();
            }
            else if (batteryProtectionLatched && lastBatteryFraction >= high)
            {
                batteryProtectionLatched = false;
                // Synchronization restarts normally; manual relay disable is respected.
            }
            batteryProtectionStatus = batteryProtectionLatched
                ? string.Format("Suspended ({0:P0})", lastBatteryFraction)
                : string.Format("Armed ({0:P0})", lastBatteryFraction);
        }

        // The stock ModuleAnimateGeneric reports progress but not direction.
        // Track the last known deployment command and correct its mid-animation label.
        private void ApplyDeploymentDirection()
        {
            if (deploymentState != QuantumRelayDeploymentState.Extending ||
                !lastDeploymentExtended.HasValue) return;
            if (!lastDeploymentExtended.Value)
                deploymentState = QuantumRelayDeploymentState.Retracting;
        }

        // Called in flight and during status refresh. Kerbalism owns the
        // failure/quality/repair UI and persistence; QR only gates operation.
        private bool IsKerbalismRelayBroken()
        {
            if (part == null || part.Modules == null)
                return false;

            foreach (PartModule module in part.Modules)
            {
                if (module == null || module.moduleName != "Reliability")
                    continue;

                // Only the Quantum Relay reliability entry may disable the
                // gateway; unrelated part failures should not do so.
                BaseField typeField = module.Fields != null
                    ? module.Fields["type"] : null;
                if (typeField == null ||
                    !string.Equals(Convert.ToString(typeField.GetValue(module)),
                        "QuantumRelay", StringComparison.OrdinalIgnoreCase))
                    continue;

                BaseField brokenField = module.Fields["broken"];
                if (brokenField != null &&
                    string.Equals(Convert.ToString(brokenField.GetValue(module)),
                        "True", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public bool IsOperational()
        {
            return operationalState ==
                   QuantumRelayOperationalState.Operational;
        }

        private void EvaluateState(bool hasPower)
        {
            if (stateMachine == null ||
                synchronizationController == null)
                return;

            QuantumRelayOperationalState previousState = operationalState;

            // Kerbalism Reliability is optional; inspect its persisted broken
            // field through KSP's PartModule API without referencing Kerbalism.dll.
            if (IsKerbalismRelayBroken())
                deploymentState = QuantumRelayDeploymentState.Broken;

            operationalState = stateMachine.Evaluate(
                relayEnabled,
                deploymentState,
                requiresCommNetHardware,
                hasCommNetHardware,
                hasPower,
                synchronizationController.IsSynchronized);

            persistedOperationalState = operationalState.ToString();

            UpdateEffectsState();

            // Reevaluate gateway selection as soon as this relay changes state.
            // This allows stronger restored gateways to reclaim the link without
            // requiring the player to switch vessels first.
            if (previousState != operationalState)
            {
                QuantumManager.Instance.NotifyRelayStateChanged(this);
                QuantumRelayCommands.RequestRefresh();
            }
        }

        /// <summary>
        /// Allows routing integrations to trigger the presentation pulse without
        /// coupling gameplay code to Unity audio/light implementation details.
        /// </summary>
        public void TriggerTransmissionPulse()
        {
            try
            {
                if (visualController != null)
                    visualController.TriggerTransmissionPulse();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Transmission effect failed without affecting relay logic: " +
                    exception.Message);
            }
        }

        private void EnsureEffectsInitialized()
        {
            if (effectsInitialized || !HighLogic.LoadedSceneIsFlight || part == null)
                return;

            effectsInitialized = true;

            try
            {
                // Use a dedicated child object. The Part's own GameObject remains
                // untouched by presentation components.
                GameObject effectsObject = new GameObject("QuantumRelayVisualController");
                effectsObject.transform.SetParent(part.transform, false);
                visualController = effectsObject.AddComponent<QuantumRelayVisualController>();

                visualController.Initialize(
                    part,
                    enableEffects,
                    activationSound,
                    transmissionSound,
                    new Vector3(
                        statusLightOffsetX,
                        statusLightOffsetY,
                        statusLightOffsetZ),
                    statusLightRange,
                    statusLightIntensity,
                    effectsVolume,
                    energyRingRadius,
                    energyRingWidth,
                    energyRingSegments,
                    energyRingTiltX,
                    energyRingTiltY,
                    energyRingTiltZ,
                    enablePerHexEmission,
                    perHexGlowRadius,
                    perHexGlowWidth,
                    perHexMaximumEmitters,
                    perHexConvergenceLines,
                    operationalState,
                    SynchronizationFraction);
            }
            catch (Exception exception)
            {
                visualController = null;
                Debug.LogWarning(
                    "[QuantumRelay] Effects initialization failed; relay gameplay remains active: " +
                    exception.Message);
            }
        }

        private void UpdateEffectsState()
        {
            try
            {
                if (visualController != null)
                    visualController.SetOperationalState(
                        operationalState,
                        SynchronizationFraction,
                        relayEnabled && statusLightPowered,
                        deploymentState == QuantumRelayDeploymentState.Extended ||
                            deploymentState == QuantumRelayDeploymentState.Fixed,
                        batteryProtectionLatched,
                        HasActiveQuantumLink(),
                        IsSynchronized);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Effects state update failed without affecting relay logic: " +
                    exception.Message);
            }
        }

        private void CheckForTransmissionPulse()
        {
            // This runs at the normal status refresh interval, never in
            // FixedUpdate. Audio inspection therefore cannot interrupt or delay
            // synchronization ticks.
            bool busy = IsAnyTransmitterBusy();
            if (busy && !transmitterWasBusy)
                TriggerTransmissionPulse();

            transmitterWasBusy = busy;
        }

        private bool IsAnyTransmitterBusy()
        {
            Vessel vessel = part != null ? part.vessel : null;
            if (vessel == null || vessel.parts == null)
                return false;

            try
            {
                foreach (Part vesselPart in vessel.parts)
                {
                    if (vesselPart == null || vesselPart.Modules == null)
                        continue;

                    foreach (PartModule module in vesselPart.Modules)
                    {
                        if (module == null ||
                            module.moduleName != QuantumRelayConstants.TransmitterModuleName)
                        {
                            continue;
                        }

                        Type type = module.GetType();
                        System.Reflection.MethodInfo isBusyMethod =
                            type.GetMethod("IsBusy", Type.EmptyTypes);

                        if (isBusyMethod != null &&
                            isBusyMethod.ReturnType == typeof(bool))
                        {
                            object result = isBusyMethod.Invoke(module, null);
                            if (result is bool && (bool)result)
                                return true;
                        }

                        System.Reflection.FieldInfo busyField =
                            type.GetField("busy") ?? type.GetField("isBusy");

                        if (busyField != null && busyField.FieldType == typeof(bool) &&
                            (bool)busyField.GetValue(module))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Unable to inspect transmitter activity: " +
                    exception.Message);
            }

            return false;
        }

        private void RefreshDiagnostics()
        {
            if (deploymentController != null)
            {
                deploymentState = deploymentController.GetState();
                ApplyDeploymentDirection();
            }

            RefreshPowerStatus();
            RefreshCommNetStatus();
        }

        private void RefreshPowerStatus()
        {
            Vessel vessel = part != null ? part.vessel : null;

            if (vessel == null)
            {
                powerStatus = "Unavailable";
                return;
            }

            try
            {
                double amount;
                double capacity;

                vessel.GetConnectedResourceTotals(
                    PartResourceLibrary.ElectricityHashcode,
                    out amount,
                    out capacity);

                powerStatus = string.Format(
                    "{0:N1} / {1:N1} EC",
                    amount,
                    capacity);
            }
            catch (Exception exception)
            {
                powerStatus = "Unavailable";

                Debug.LogWarning(
                    "[QuantumRelay] Unable to read Electric Charge: " +
                    exception.Message);
            }
        }

        private void RefreshCommNetStatus()
        {
            Vessel vessel = part != null ? part.vessel : null;

            if (vessel == null)
            {
                hasCommNetHardware = false;
                commNetStatus = "Unavailable";
                return;
            }

            if (!requiresCommNetHardware)
            {
                hasCommNetHardware = true;
                commNetStatus = "Not Required";
                return;
            }

            try
            {
                string evidence;
                hasCommNetHardware =
                    GatewayScanner.HasCommNetCapability(
                        vessel,
                        out evidence);

                // Hardware detection is an internal prerequisite, not the PAW
                // communication-availability indicator. UpdateDisplayFields owns it.
            }
            catch (Exception exception)
            {
                hasCommNetHardware = false;
                commNetStatus = "Unavailable";

                Debug.LogWarning(
                    "[QuantumRelay] Unable to inspect CommNet capability: " +
                    exception.Message);
            }
        }

        private void UpdateDisplayFields()
        {
            relayModelStatus = relayModel;
            relayStatus =
                RelayStateMachine.GetDisplayName(operationalState);
            connectedPartnerStatus = "No Partner Connected";
            quantumSignalStatus = "Unavailable";
            try
            {
                Vessel ownVessel = part != null ? part.vessel : null;
                if (ownVessel != null)
                {
                    foreach (NetworkTelemetry network in QuantumRelayRegistry.Networks)
                    {
                        if (network == null || !network.Online ||
                            network.GatewayA == null || network.GatewayB == null)
                            continue;
                        GatewayTelemetry other = null;
                        if (network.GatewayA.VesselId == ownVessel.id)
                            other = network.GatewayB;
                        else if (network.GatewayB.VesselId == ownVessel.id)
                            other = network.GatewayA;
                        if (other == null) continue;
                        connectedPartnerStatus = other.VesselName ?? "Unknown";
                        double strength = Math.Min(
                            network.GatewayA.HasQuantumRelayModule ? network.GatewayA.RelaySignalStrength : 0.25,
                            network.GatewayB.HasQuantumRelayModule ? network.GatewayB.RelaySignalStrength : 0.25);
                        quantumSignalStatus = string.Format("{0:0.#}%", strength * 100.0);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QuantumRelay] Partner PAW telemetry unavailable: " + ex.Message);
            }
            bool partnerLinked = HasActiveQuantumLink();
            // Show actual communications availability, not hardware detection or
            // local quantum synchronization. A B-side relay without a route is
            // unavailable until the quantum bridge establishes a valid link.
            bool stockCommNetAvailable = false;
            try
            {
                Vessel vessel = part != null ? part.vessel : null;
                stockCommNetAvailable = vessel != null &&
                    vessel.Connection != null && vessel.Connection.IsConnected;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[QuantumRelay] Unable to read vessel CommNet connection: " +
                    exception.Message);
            }
            commNetStatus = (stockCommNetAvailable || partnerLinked)
                ? "Available" : "Unavailable";
            bool searching = relayEnabled && !batteryProtectionLatched &&
                (deploymentState == QuantumRelayDeploymentState.Extended ||
                 deploymentState == QuantumRelayDeploymentState.Fixed) &&
                IsSynchronized && !partnerLinked;
            if (batteryProtectionLatched)
                relayStatus = "Power Saving";
            else if (searching)
                relayStatus = "Disconnected";
            deploymentStatus =
                RelayDeploymentController.GetDisplayName(deploymentState);
            connectionStatus = batteryProtectionLatched ? "Waiting for Battery Recovery" :
                searching ? "Searching for Partner" :
                partnerLinked ? "Locked" : RelayStateMachine.GetDisplayName(operationalState);
            operationalStatus = IsOperational() && partnerLinked &&
                !batteryProtectionLatched ? "YES" : "NO";

            powerUsageStatus = string.Format(
                "{0:N2} EC/s",
                CurrentPowerRate + (statusLightPowered ? StatusLightPowerRate : 0.0));

            if (batteryProtectionLatched)
            {
                synchronizationStatus = "Suspended - Low Battery";
            }
            else if (searching)
            {
                synchronizationStatus = "Quantum Link Unavailable";
            }
            else if (!relayEnabled &&
                (deploymentState == QuantumRelayDeploymentState.Extended ||
                 deploymentState == QuantumRelayDeploymentState.Fixed))
            {
                relayStatus = "Relay Disabled";
                synchronizationStatus =
                    "Enable Quantum Relay to begin synchronization";
            }
            else if (IsSynchronized)
            {
                synchronizationStatus = GetStartupCompleteText();
            }
            else if (
                operationalState ==
                QuantumRelayOperationalState.Synchronizing)
            {
                synchronizationStatus = string.Format(
                    "{0} ({1:P0})",
                    GetStartupStageText(SynchronizationFraction),
                    SynchronizationFraction);
            }
            else
            {
                synchronizationStatus = string.Format(
                    "Not Synchronized ({0:P0})",
                    SynchronizationFraction);
            }
        }

        private string GetStartupStageText(double progress)
        {
            if (progress < 0.25)
                return GetStartupStage(1);

            if (progress < 0.50)
                return GetStartupStage(2);

            if (progress < 0.75)
                return GetStartupStage(3);

            return GetStartupStage(4);
        }

        private string GetStartupCompleteText()
        {
            if (!string.IsNullOrEmpty(startupComplete))
                return startupComplete;

            switch (RelayTier)
            {
                case 1:
                    return "Quantum Link Established";

                case 2:
                    return "Voyager Link Established";

                case 3:
                    return "Event Horizon Stable";

                case 4:
                    return "Horizon Prime Network Stable";

                default:
                    return "Synchronized";
            }
        }

        private string GetStartupStage(int stage)
        {
            string configuredStage = GetConfiguredStartupStage(stage);

            if (!string.IsNullOrEmpty(configuredStage))
                return configuredStage;

            switch (RelayTier)
            {
                case 1:
                    switch (stage)
                    {
                        case 1:
                            return "Deploying Reflector";
                        case 2:
                            return "Charging Capacitors";
                        case 3:
                            return "Calibrating Field";
                        default:
                            return "Synchronizing";
                    }

                case 2:
                    switch (stage)
                    {
                        case 1:
                            return "Initializing Relay";
                        case 2:
                            return "Charging Quantum Matrix";
                        case 3:
                            return "Calibrating Entanglement";
                        default:
                            return "Synchronizing";
                    }

                case 3:
                    switch (stage)
                    {
                        case 1:
                            return "Quantum Core Online";
                        case 2:
                            return "Establishing Entanglement";
                        case 3:
                            return "Stabilizing Event Horizon";
                        default:
                            return "Locking Quantum Bridge";
                    }

                case 4:
                    switch (stage)
                    {
                        case 1:
                            return "Horizon Prime Core Online";
                        case 2:
                            return "Harmonizing Quantum Bands";
                        case 3:
                            return "Stabilizing Entanglement Lattice";
                        default:
                            return "Opening Prime Bridge";
                    }

                default:
                    return "Synchronizing";
            }
        }

        private string GetConfiguredStartupStage(int stage)
        {
            switch (stage)
            {
                case 1:
                    return startupStage1;
                case 2:
                    return startupStage2;
                case 3:
                    return startupStage3;
                case 4:
                    return startupStage4;
                default:
                    return "";
            }
        }


        private void ApplyOfficialRelayDefinition()
        {
            RelayClass parsedClass = RelayCatalog.Parse(relayClass, 1);
            relayDefinition = RelayCatalog.Get(parsedClass);
            relayClass = relayDefinition.RelayClass.ToString();
            relayModel = relayDefinition.DisplayName;
        }

        private void UpdateEventVisibility()
        {
            BaseEvent enableEvent = Events["EnableRelay"];
            BaseEvent disableEvent = Events["DisableRelay"];
            BaseEvent extendEvent = Events["ExtendRelay"];
            BaseEvent retractEvent = Events["RetractRelay"];
            BaseEvent resetEvent = Events["ResetSynchronizationEvent"];

            if (enableEvent != null)
                enableEvent.active = !relayEnabled;

            if (disableEvent != null)
                disableEvent.active = relayEnabled;

            bool canControlDeployment =
                requiresDeployment &&
                deploymentState != QuantumRelayDeploymentState.Missing &&
                deploymentState != QuantumRelayDeploymentState.Fixed &&
                deploymentState != QuantumRelayDeploymentState.Broken;

            if (extendEvent != null)
            {
                extendEvent.active =
                    canControlDeployment &&
                    (deploymentState ==
                         QuantumRelayDeploymentState.Retracted ||
                     deploymentState ==
                         QuantumRelayDeploymentState.Unknown);
            }

            if (retractEvent != null)
            {
                retractEvent.active =
                    canControlDeployment &&
                    deploymentState ==
                        QuantumRelayDeploymentState.Extended;
            }

            if (resetEvent != null)
            {
                resetEvent.active =
                    IsSynchronized ||
                    SynchronizationFraction > 0.0001;
            }
        }

        private void ResetSynchronization()
        {
            if (synchronizationController != null)
                synchronizationController.Reset();

            synchronizationProgress = 0.0;
            relaySynchronized = false;
        }

        private void PersistSynchronizationState()
        {
            synchronizationProgress =
                synchronizationController.Progress;
            relaySynchronized =
                synchronizationController.IsSynchronized;
        }

        private void ForceRefresh()
        {
            RefreshDiagnostics();
            EvaluateState(
                powerController == null || powerController.HasPower);
            UpdateDisplayFields();
            UpdateEventVisibility();
            nextStatusRefreshTime = 0.0;

            // Player actions such as enable, disable, extend, and retract should
            // immediately rebuild the gateway inventory.
            QuantumRelayCommands.RequestRefresh();
        }
    }
}
