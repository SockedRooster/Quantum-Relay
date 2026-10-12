using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Controls the stock ModuleResourceConverter instances used for relay power.
    /// Using stock converters allows Dynamic Battery Storage and other system
    /// monitors to identify and display Quantum Relay EC consumption.
    /// </summary>
    internal sealed class RelayPowerController
    {
        private const double MinimumReceivedFraction = 0.999;
        private const double SmallAmount = 0.0001;

        private const string IdleConverterName =
            "Quantum Relay Standby Power";
        private const string SynchronizationConverterName =
            "Quantum Relay Synchronization Power";
        private const string OperationalConverterName =
            "Quantum Relay Operational Power";

        private ModuleResourceConverter idleConverter;
        private ModuleResourceConverter synchronizationConverter;
        private ModuleResourceConverter operationalConverter;

        // Editor-only selection. Reasserted when another mod refreshes converter states.
        private int editorPreviewMode = -1;
        // Kerbalism ProcessController toggles, accessed without linking Kerbalism assemblies.
        private bool kerbalismPlanner;

        // Kerbalism's editor planner may sum INPUT_RESOURCE ratios even for
        // deactivated converters. Keep original values so switching modes is
        // reversible and never change flight converter definitions.
        private readonly Dictionary<ModuleResourceConverter, double> editorBaseRates =
            new Dictionary<ModuleResourceConverter, double>();

        public string EditorPreviewStatus
        {
            get
            {
                if (kerbalismPlanner) return PreviewModeName + " (Kerbalism planner API)";
                if (!UsesStockConverters)
                    return PreviewModeName + " (no backend)";

                string selected = editorPreviewMode == 0 ? "Standby" :
                    editorPreviewMode == 1 ? "Synchronization" :
                    editorPreviewMode == 2 ? "Operational" : "Off";
                bool idle = idleConverter != null && idleConverter.IsActivated;
                bool sync = synchronizationConverter != null && synchronizationConverter.IsActivated;
                bool operational = operationalConverter != null && operationalConverter.IsActivated;
                string active = operational ? "Operational" :
                    sync ? "Synchronization" : idle ? "Standby" : "Off";
                return selected + " (converter: " + active + ")";
            }
        }

        public int EditorPreviewMode { get { return editorPreviewMode; } }

        public string PreviewModeName
        {
            get { return editorPreviewMode == 0 ? "Standby" :
                editorPreviewMode == 1 ? "Synchronization" :
                editorPreviewMode == 2 ? "Operational" : "Off"; }
        }

        // Keep PAW diagnostics short. Full module list is emitted to KSP.log.
        public string GetModuleInventory(Part part)
        {
            if (part == null || part.Modules == null)
                return "No part";
            int stock = 0;
            int processes = 0;
            foreach (PartModule module in part.Modules)
            {
                if (module == null) continue;
                string name = module.moduleName ?? module.GetType().Name;
                if (module is ModuleResourceConverter) stock++;
                if (name.IndexOf("ProcessController", StringComparison.OrdinalIgnoreCase) >= 0)
                    processes++;
            }
            return "Stock " + stock + " / Kerbalism " + processes;
        }

        private static string GetDetailedModuleInventory(Part part)
        {
            if (part == null || part.Modules == null) return "No modules";
            var names = new List<string>();
            foreach (PartModule module in part.Modules)
            {
                if (module == null) continue;
                string name = module.moduleName ?? module.GetType().Name;
                var converter = module as ModuleResourceConverter;
                if (converter != null)
                    name += " [" + (converter.ConverterName ?? "unnamed") + "]";
                names.Add(name);
            }
            return string.Join(" | ", names.ToArray());
        }

        public double CurrentRate { get; private set; }
        public bool HasPower { get; private set; } = true;
        public bool UsesStockConverters { get; private set; }

        public void Configure(Part part)
        {
            editorBaseRates.Clear();
            kerbalismPlanner = false;
            foreach (var loaded in AssemblyLoader.loadedAssemblies)
                if (loaded.assembly.GetType("KERBALISM.ProcessController") != null)
                    kerbalismPlanner = true;
            idleConverter = null;
            synchronizationConverter = null;
            operationalConverter = null;
            UsesStockConverters = false;

            if (part == null)
                return;

            List<ModuleResourceConverter> converters =
                part.FindModulesImplementing<ModuleResourceConverter>();

            if (converters == null)
                return;

            for (int index = 0; index < converters.Count; index++)
            {
                ModuleResourceConverter converter = converters[index];

                if (converter == null)
                    continue;

                if (string.Equals(
                    converter.ConverterName,
                    IdleConverterName,
                    StringComparison.Ordinal))
                {
                    idleConverter = converter;
                }
                else if (string.Equals(
                    converter.ConverterName,
                    SynchronizationConverterName,
                    StringComparison.Ordinal))
                {
                    synchronizationConverter = converter;
                }
                else if (string.Equals(
                    converter.ConverterName,
                    OperationalConverterName,
                    StringComparison.Ordinal))
                {
                    operationalConverter = converter;
                }
            }

            UsesStockConverters =
                idleConverter != null &&
                synchronizationConverter != null &&
                operationalConverter != null;

            ConfigureConverterInterface(idleConverter);
            ConfigureConverterInterface(synchronizationConverter);
            ConfigureConverterInterface(operationalConverter);

            StopAllConverters();
            if (HighLogic.LoadedSceneIsEditor)
            {
                RememberEditorRate(idleConverter);
                RememberEditorRate(synchronizationConverter);
                RememberEditorRate(operationalConverter);
                ApplyEditorPreview();
            }

            if (!UsesStockConverters)
            {
                Debug.LogWarning("[QuantumRelay] Loaded part modules: " + GetDetailedModuleInventory(part));
                Debug.LogWarning(
                    "[QuantumRelay] Stock power converters were not found. " +
                    "Falling back to direct ElectricCharge consumption.");
            }
        }

        // VAB-only manual selection for Kerbalism's resource simulator.
        // Never used during flight; Consume() owns flight converter switching.
        public void SetEditorPreview(int mode)
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;

            int selected = mode >= 0 && mode <= 2 ? mode : -1;
            if (selected == editorPreviewMode) return;
            editorPreviewMode = selected;
            ApplyEditorPreview();
            if (EditorLogic.fetch != null && EditorLogic.fetch.ship != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        public void ApplyEditorPreview()
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;

            RefreshPowerInterface();
            // Kerbalism receives EC from PlannerUpdate; stock inputs must all
            // be zero in the editor to avoid counting the same load twice.
            SetEditorRate(idleConverter, !kerbalismPlanner && editorPreviewMode == 0);
            SetEditorRate(synchronizationConverter, !kerbalismPlanner && editorPreviewMode == 1);
            SetEditorRate(operationalConverter, !kerbalismPlanner && editorPreviewMode == 2);

            // Do not repeatedly stop/start converters already in the desired state.
            if (editorPreviewMode != 0) SetConverterActive(idleConverter, false);
            if (editorPreviewMode != 1) SetConverterActive(synchronizationConverter, false);
            if (editorPreviewMode != 2) SetConverterActive(operationalConverter, false);
            if (editorPreviewMode == 0)
                SetConverterActive(idleConverter, true);
            else if (editorPreviewMode == 1)
                SetConverterActive(synchronizationConverter, true);
            else if (editorPreviewMode == 2)
                SetConverterActive(operationalConverter, true);
        }

        private void RememberEditorRate(ModuleResourceConverter converter)
        {
            if (converter == null || converter.inputList == null) return;
            foreach (ResourceRatio input in converter.inputList)
            {
                if (input.ResourceName == "ElectricCharge")
                {
                    editorBaseRates[converter] = input.Ratio;
                    return;
                }
            }
        }

        private void SetEditorRate(ModuleResourceConverter converter, bool selected)
        {
            if (converter == null || converter.inputList == null) return;
            double original;
            if (!editorBaseRates.TryGetValue(converter, out original)) return;
            for (int i = 0; i < converter.inputList.Count; i++)
            {
                ResourceRatio input = converter.inputList[i];
                if (input.ResourceName != "ElectricCharge") continue;
                input.Ratio = selected ? original : 0.0;
                converter.inputList[i] = input;
            }
        }

        public double GetRequiredRate(
            QuantumRelayOperationalState state,
            bool relayEnabled,
            double idleRate,
            double synchronizationRate,
            double operationalRate)
        {
            switch (state)
            {
                case QuantumRelayOperationalState.Synchronizing:
                    return Math.Max(0.0, synchronizationRate);

                case QuantumRelayOperationalState.Operational:
                    return Math.Max(0.0, operationalRate);

                case QuantumRelayOperationalState.Deploying:
                case QuantumRelayOperationalState.Retracting:
                case QuantumRelayOperationalState.Retracted:
                case QuantumRelayOperationalState.NoCommNetHardware:
                case QuantumRelayOperationalState.HardwareFault:
                    return relayEnabled ? Math.Max(0.0, idleRate) : 0.0;

                default:
                    return 0.0;
            }
        }

        public bool Consume(
            Part part,
            QuantumRelayOperationalState state,
            double rate,
            double deltaTime)
        {
            CurrentRate = Math.Max(0.0, rate);

            if (part == null || CurrentRate <= 0.0 || deltaTime <= 0.0)
            {
                StopAllConverters();
                HasPower = true;
                return true;
            }

            if (UsesStockConverters)
            {
                ActivateConverterForState(state);
                HasPower = HasAvailableCharge(part, CurrentRate, deltaTime);
                return HasPower;
            }

            // Compatibility fallback for vessels or legacy part configs that do
            // not contain the stock converter modules.
            double requested = CurrentRate * deltaTime;
            double received = part.RequestResource(
                PartResourceLibrary.ElectricityHashcode,
                requested);

            HasPower =
                requested <= SmallAmount ||
                received / requested >= MinimumReceivedFraction;

            return HasPower;
        }

        public void Shutdown()
        {
            CurrentRate = 0.0;
            HasPower = true;
            StopAllConverters();
        }

        private bool HasAvailableCharge(
            Part part,
            double rate,
            double deltaTime)
        {
            Vessel vessel = part != null ? part.vessel : null;

            if (vessel == null)
                return false;

            try
            {
                double amount;
                double capacity;

                vessel.GetConnectedResourceTotals(
                    PartResourceLibrary.ElectricityHashcode,
                    out amount,
                    out capacity);

                double required = rate * deltaTime;
                return required <= SmallAmount ||
                       amount / required >= MinimumReceivedFraction;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Unable to inspect ElectricCharge: " +
                    exception.Message);
                return false;
            }
        }

        private void ActivateConverterForState(
            QuantumRelayOperationalState state)
        {
            ModuleResourceConverter target = null;

            switch (state)
            {
                case QuantumRelayOperationalState.Synchronizing:
                    target = synchronizationConverter;
                    break;

                case QuantumRelayOperationalState.Operational:
                    target = operationalConverter;
                    break;

                case QuantumRelayOperationalState.Deploying:
                case QuantumRelayOperationalState.Retracting:
                case QuantumRelayOperationalState.Retracted:
                case QuantumRelayOperationalState.NoCommNetHardware:
                case QuantumRelayOperationalState.HardwareFault:
                    target = idleConverter;
                    break;
            }

            // Shut down every non-target converter BEFORE starting the target.
            // The previous implementation could start the target before stopping
            // an older converter, allowing overlapping consumption during a tick.
            if (idleConverter != target)
                SetConverterActive(idleConverter, false);
            if (synchronizationConverter != target)
                SetConverterActive(synchronizationConverter, false);
            if (operationalConverter != target)
                SetConverterActive(operationalConverter, false);

            if (target != null)
                SetConverterActive(target, true);
        }

        private void StopAllConverters()
        {
            SetConverterActive(idleConverter, false);
            SetConverterActive(synchronizationConverter, false);
            SetConverterActive(operationalConverter, false);
        }

        private static void SetConverterActive(
            ModuleResourceConverter converter,
            bool active)
        {
            if (converter == null)
                return;

            try
            {
                if (active)
                {
                    if (!converter.IsActivated)
                        converter.StartResourceConverter();
                }
                else if (converter.IsActivated)
                {
                    converter.StopResourceConverter();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Unable to change power converter state: " +
                    exception.Message);
            }
        }

        public void RefreshPowerInterface()
        {
            ConfigureConverterInterface(idleConverter);
            ConfigureConverterInterface(synchronizationConverter);
            ConfigureConverterInterface(operationalConverter);
        }

        private static void ConfigureConverterInterface(
            ModuleResourceConverter converter)
        {
            if (converter == null)
                return;

            HideEvent(converter, "StartResourceConverter");
            HideEvent(converter, "StopResourceConverter");
            HideEvent(converter, "ToggleResourceConverter");
            foreach (BaseAction action in converter.Actions) action.active = false;

            BaseField statusField = converter.Fields["status"];
            if (statusField != null)
            {
                statusField.guiActive = false;
                statusField.guiActiveEditor = false;
            }
        }

        private static void HideEvent(
            PartModule module,
            string eventName)
        {
            BaseEvent moduleEvent = module.Events[eventName];

            if (moduleEvent == null)
                return;

            // Keep only Quantum Relay's own editor preview buttons visible.
            // The stock converter events duplicate those controls.
            moduleEvent.active = false;
            moduleEvent.guiActive = false;
            moduleEvent.guiActiveEditor = false;
        }
    }
}
