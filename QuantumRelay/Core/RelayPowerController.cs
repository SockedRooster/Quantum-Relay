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

        public double CurrentRate { get; private set; }
        public bool HasPower { get; private set; } = true;
        public bool UsesStockConverters { get; private set; }

        public void Configure(Part part)
        {
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

            HideConverterInterface(idleConverter);
            HideConverterInterface(synchronizationConverter);
            HideConverterInterface(operationalConverter);

            StopAllConverters();

            if (!UsesStockConverters)
            {
                Debug.LogWarning(
                    "[QuantumRelay] Stock power converters were not found. " +
                    "Falling back to direct ElectricCharge consumption.");
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

            SetConverterActive(idleConverter, target == idleConverter);
            SetConverterActive(
                synchronizationConverter,
                target == synchronizationConverter);
            SetConverterActive(
                operationalConverter,
                target == operationalConverter);
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

        private static void HideConverterInterface(
            ModuleResourceConverter converter)
        {
            if (converter == null)
                return;

            HideEvent(converter, "StartResourceConverter");
            HideEvent(converter, "StopResourceConverter");
            HideEvent(converter, "ToggleResourceConverter");

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

            moduleEvent.active = false;
            moduleEvent.guiActive = false;
            moduleEvent.guiActiveEditor = false;
        }
    }
}
