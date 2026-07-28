using System;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Isolates all relay presentation from gameplay. Any exception raised here
    /// is contained by ModuleQuantumRelay and can never stop synchronization.
    /// </summary>
    internal sealed class QuantumRelayVisualController : MonoBehaviour
    {
        private QuantumRelayEffects _renderer;
        private RelayVisualState _state = RelayVisualState.Folded;
        private float _syncFraction;
        private bool _initialized;

        public RelayVisualState State
        {
            get { return _state; }
        }

        public void Initialize(
            Part part,
            bool effectsEnabled,
            string activationClipPath,
            string transmissionClipPath,
            Vector3 effectOffset,
            float lightRange,
            float lightIntensity,
            float audioVolume,
            float ringRadius,
            float ringWidth,
            int ringSegments,
            float ringTiltX,
            float ringTiltY,
            float ringTiltZ,
            bool enablePerHexEmission,
            float perHexGlowRadius,
            float perHexGlowWidth,
            int perHexMaximumEmitters,
            bool perHexConvergenceLines,
            QuantumRelayOperationalState initialOperationalState,
            double initialSynchronization)
        {
            GameObject rendererObject = new GameObject("QuantumRelayVisualRenderer");
            rendererObject.transform.SetParent(transform, false);

            _renderer = rendererObject.AddComponent<QuantumRelayEffects>();
            _renderer.Initialize(
                part,
                effectsEnabled,
                activationClipPath,
                transmissionClipPath,
                effectOffset,
                lightRange,
                lightIntensity,
                audioVolume,
                ringRadius,
                ringWidth,
                ringSegments,
                new Vector3(ringTiltX, ringTiltY, ringTiltZ),
                enablePerHexEmission,
                perHexGlowRadius,
                perHexGlowWidth,
                perHexMaximumEmitters,
                perHexConvergenceLines);

            _syncFraction = Mathf.Clamp01((float)initialSynchronization);
            _state = MapState(initialOperationalState);
            _renderer.SetVisualState(_state, _syncFraction, true);
            _initialized = true;
        }

        public void SetOperationalState(
            QuantumRelayOperationalState operationalState,
            double synchronizationFraction)
        {
            if (!_initialized || _renderer == null)
                return;

            _syncFraction = Mathf.Clamp01((float)synchronizationFraction);
            RelayVisualState next = MapState(operationalState);
            bool changed = next != _state;
            _state = next;
            _renderer.SetVisualState(_state, _syncFraction, changed);
        }

        public void TriggerTransmissionPulse()
        {
            if (!_initialized || _renderer == null)
                return;

            _renderer.TriggerTransmissionPulse();
        }

        private static RelayVisualState MapState(QuantumRelayOperationalState state)
        {
            switch (state)
            {
                case QuantumRelayOperationalState.Disabled:
                    return RelayVisualState.Standby;

                case QuantumRelayOperationalState.Synchronizing:
                case QuantumRelayOperationalState.Deploying:
                    return RelayVisualState.Initializing;

                case QuantumRelayOperationalState.Operational:
                    return RelayVisualState.Entangled;

                case QuantumRelayOperationalState.InsufficientPower:
                case QuantumRelayOperationalState.NoCommNetHardware:
                case QuantumRelayOperationalState.HardwareFault:
                    return RelayVisualState.Fault;

                case QuantumRelayOperationalState.Retracted:
                case QuantumRelayOperationalState.Retracting:
                case QuantumRelayOperationalState.Unknown:
                default:
                    return RelayVisualState.Folded;
            }
        }
    }
}
