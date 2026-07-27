using System;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Presentation-only controller for Quantum Relay light and audio effects.
    /// Gameplay logic owns the relay state; this component only renders it.
    /// </summary>
    internal sealed class QuantumRelayEffects : MonoBehaviour
    {
        private Part _part;
        private Light _statusLight;
        private AudioSource _activationSource;
        private AudioSource _transmissionSource;
        private QuantumRelayOperationalState _state = QuantumRelayOperationalState.Unknown;
        private float _stateChangedAt;
        private float _transmissionFlashUntil;
        private float _nextTransmissionAllowed;
        private bool _initialized;

        private string _activationClipPath;
        private string _transmissionClipPath;
        private Vector3 _lightOffset;
        private float _lightRange;
        private float _lightIntensity;
        private float _audioVolume;
        private bool _effectsEnabled;

        public void Initialize(
            Part part,
            bool effectsEnabled,
            string activationClipPath,
            string transmissionClipPath,
            Vector3 lightOffset,
            float lightRange,
            float lightIntensity,
            float audioVolume,
            QuantumRelayOperationalState initialState)
        {
            _part = part;
            _effectsEnabled = effectsEnabled;
            _activationClipPath = activationClipPath;
            _transmissionClipPath = transmissionClipPath;
            _lightOffset = lightOffset;
            _lightRange = Mathf.Max(0.1f, lightRange);
            _lightIntensity = Mathf.Max(0.0f, lightIntensity);
            _audioVolume = Mathf.Clamp01(audioVolume);

            CreateLight();
            CreateAudioSources();
            _state = initialState;
            _stateChangedAt = Time.time;
            _initialized = true;
            ApplyImmediateState();
        }

        public void SetState(QuantumRelayOperationalState state)
        {
            if (!_initialized || state == _state)
                return;

            QuantumRelayOperationalState previous = _state;
            _state = state;
            _stateChangedAt = Time.time;

            // Activation is intentionally transition-driven so loading a vessel
            // that was already online does not replay the sound.
            if (state == QuantumRelayOperationalState.Operational &&
                previous != QuantumRelayOperationalState.Operational)
            {
                PlayOneShot(_activationSource);
            }
        }

        public void TriggerTransmissionPulse()
        {
            if (!_initialized || !_effectsEnabled ||
                _state != QuantumRelayOperationalState.Operational ||
                Time.time < _nextTransmissionAllowed)
            {
                return;
            }

            _nextTransmissionAllowed = Time.time + 0.75f;
            _transmissionFlashUntil = Time.time + 0.22f;
            PlayOneShot(_transmissionSource);
        }

        private void Update()
        {
            if (!_initialized || _statusLight == null)
                return;

            if (!_effectsEnabled)
            {
                _statusLight.enabled = false;
                return;
            }

            float intensity;
            Color color;
            GetVisualState(out intensity, out color);

            if (Time.time < _transmissionFlashUntil)
                intensity = Mathf.Max(intensity, _lightIntensity * 1.8f);

            _statusLight.color = color;
            _statusLight.intensity = Mathf.Max(0.0f, intensity);
            _statusLight.enabled = intensity > 0.005f;
        }

        private void GetVisualState(out float intensity, out Color color)
        {
            float elapsed = Mathf.Max(0.0f, Time.time - _stateChangedAt);
            color = new Color(0.20f, 0.65f, 1.00f, 1.0f);

            switch (_state)
            {
                case QuantumRelayOperationalState.Deploying:
                case QuantumRelayOperationalState.Retracting:
                    intensity = _lightIntensity * Mathf.Clamp01(elapsed / 2.0f) * 0.65f;
                    return;

                case QuantumRelayOperationalState.Synchronizing:
                    intensity = _lightIntensity * (0.35f + 0.45f * Pulse(elapsed, 1.0f));
                    return;

                case QuantumRelayOperationalState.Operational:
                    intensity = _lightIntensity * (0.45f + 0.25f * Pulse(elapsed, 3.0f));
                    return;

                case QuantumRelayOperationalState.InsufficientPower:
                case QuantumRelayOperationalState.NoCommNetHardware:
                case QuantumRelayOperationalState.HardwareFault:
                    color = new Color(1.00f, 0.18f, 0.10f, 1.0f);
                    intensity = _lightIntensity * (0.08f + 0.24f * Pulse(elapsed, 3.5f));
                    return;

                default:
                    intensity = 0.0f;
                    return;
            }
        }

        private static float Pulse(float elapsed, float period)
        {
            float safePeriod = Mathf.Max(0.1f, period);
            return 0.5f + 0.5f * Mathf.Sin((elapsed / safePeriod) * Mathf.PI * 2.0f);
        }

        private void CreateLight()
        {
            GameObject lightObject = new GameObject("QuantumRelayStatusLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = _lightOffset;

            _statusLight = lightObject.AddComponent<Light>();
            _statusLight.type = LightType.Point;
            _statusLight.range = _lightRange;
            _statusLight.shadows = LightShadows.None;
            _statusLight.renderMode = LightRenderMode.ForcePixel;
            _statusLight.enabled = false;
        }

        private void CreateAudioSources()
        {
            _activationSource = CreateAudioSource("QuantumRelayActivationAudio", _activationClipPath);
            _transmissionSource = CreateAudioSource("QuantumRelayTransmissionAudio", _transmissionClipPath);
        }

        private AudioSource CreateAudioSource(string objectName, string clipPath)
        {
            GameObject audioObject = new GameObject(objectName);
            audioObject.transform.SetParent(transform, false);
            audioObject.transform.localPosition = _lightOffset;

            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1.0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2.0f;
            source.maxDistance = 80.0f;
            source.dopplerLevel = 0.0f;
            source.volume = _audioVolume;

            if (!string.IsNullOrEmpty(clipPath))
            {
                try
                {
                    source.clip = GameDatabase.Instance.GetAudioClip(clipPath);
                    if (source.clip == null)
                        Debug.LogWarning("[QuantumRelay] Audio clip not found: " + clipPath);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[QuantumRelay] Unable to load audio clip " + clipPath + ": " + exception.Message);
                }
            }

            return source;
        }

        private void PlayOneShot(AudioSource source)
        {
            if (!_effectsEnabled || source == null || source.clip == null)
                return;

            source.Stop();
            source.Play();
        }

        private void ApplyImmediateState()
        {
            if (_statusLight == null)
                return;

            float intensity;
            Color color;
            GetVisualState(out intensity, out color);
            _statusLight.color = color;
            _statusLight.intensity = intensity;
            _statusLight.enabled = _effectsEnabled && intensity > 0.005f;
        }

        private void OnDestroy()
        {
            if (_activationSource != null)
                _activationSource.Stop();
            if (_transmissionSource != null)
                _transmissionSource.Stop();
        }
    }
}
