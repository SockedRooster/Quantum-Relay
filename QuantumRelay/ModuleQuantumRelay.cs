using System;
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
        public int relayTier = 1;

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
        public double signalStrength = 1.0;

        [KSPField]
        public int maxWormholes = 1;

        [KSPField]
        public double deploymentLossGracePeriod = 2.0;

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

        [KSPField(
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Quantum Relay")]
        public string relayStatus = "Standby";

        [KSPField(
            guiActive = true,
            guiActiveEditor = true,
            guiName = "Relay Model")]
        public string relayModelStatus = "Quantum Relay";

        [KSPField(guiActive = true, guiName = "Deployment")]
        public string deploymentStatus = "Unknown";

        [KSPField(guiActive = true, guiName = "Synchronization")]
        public string synchronizationStatus = "Not Synchronized";

        [KSPField(guiActive = true, guiName = "Power Usage")]
        public string powerUsageStatus = "0.00 EC/s";

        [KSPField(guiActive = true, guiName = "Operational")]
        public string operationalStatus = "NO";

        [KSPField(guiActive = true, guiName = "Electric Charge")]
        public string powerStatus = "Unavailable";

        [KSPField(guiActive = true, guiName = "CommNet")]
        public string commNetStatus = "Not Detected";

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

        public double SignalStrengthMultiplier
        {
            get { return Math.Max(0.0, Math.Min(1.0, signalStrength)); }
        }

        public string OperationalStateName
        {
            get { return operationalState.ToString(); }
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
                deploymentController.Invoke(true);

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
                deploymentController.Invoke(false);

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
                deploymentController.Invoke(true);
            }
            else if (
                deploymentState == QuantumRelayDeploymentState.Extended ||
                deploymentState == QuantumRelayDeploymentState.Extending)
            {
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
            EvaluateState(true);
            UpdateDisplayFields();
            UpdateEventVisibility();
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            if (Time.time < nextStatusRefreshTime)
                return;

            nextStatusRefreshTime =
                Time.time + Math.Max(0.05, statusRefreshInterval);

            RefreshDiagnostics();
            EvaluateState(true);
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

            double requiredRate = powerController.GetRequiredRate(
                prePowerState,
                relayEnabled,
                idlePowerRate,
                synchronizationPowerRate,
                operationalPowerRate);

            bool hasPower = powerController.Consume(
                part,
                prePowerState,
                requiredRate,
                Math.Max(0.0, TimeWarp.fixedDeltaTime));

            bool canSynchronize =
                relayEnabled &&
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
            EvaluateState(hasPower);
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

            operationalState = stateMachine.Evaluate(
                relayEnabled,
                deploymentState,
                requiresCommNetHardware,
                hasCommNetHardware,
                hasPower,
                synchronizationController.IsSynchronized);

            persistedOperationalState = operationalState.ToString();
        }

        private void RefreshDiagnostics()
        {
            if (deploymentController != null)
                deploymentState = deploymentController.GetState();

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

                commNetStatus = hasCommNetHardware
                    ? "Detected"
                    : "Not Detected";
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
            deploymentStatus =
                RelayDeploymentController.GetDisplayName(deploymentState);
            operationalStatus = IsOperational() ? "YES" : "NO";

            powerUsageStatus = string.Format(
                "{0:N2} EC/s",
                CurrentPowerRate);

            if (IsSynchronized)
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

            switch (relayTier)
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

            switch (relayTier)
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
        }
    }
}