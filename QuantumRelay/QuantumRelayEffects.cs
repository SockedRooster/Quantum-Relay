using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumRelay
{
    /// <summary>
    /// Presentation renderer for Quantum Relay. It draws a segmented energy ring,
    /// outward radiating pulse rings, a nearby status glow, and spatial audio.
    /// It never reads or changes relay gameplay state.
    /// </summary>
    internal sealed class QuantumRelayEffects : MonoBehaviour
    {
        private const int MaximumSegments = 48;
        private const int PulseRingCount = 3;
        private const int PerHexRingPoints = 24;

        private Light _statusLight;
        private AudioSource _activationSource;
        private AudioSource _transmissionSource;
        private LineRenderer[] _segments;
        private LineRenderer[] _pulseRings;
        private Material _lineMaterial;
        private readonly List<PerHexEmitter> _perHexEmitters = new List<PerHexEmitter>();
        private bool _enablePerHexEmission;
        private float _perHexGlowRadius;
        private float _perHexGlowWidth;
        private int _perHexMaximumEmitters;
        private bool _perHexConvergenceLines;
        private float _nextPanelDiscoveryAt;

        private RelayVisualState _state = RelayVisualState.Folded;
        private float _syncFraction;
        private float _stateChangedAt;
        private float _transmissionUntil;
        private float _nextTransmissionAllowed;
        private bool _initialized;
        private bool _effectsEnabled;

        private Vector3 _effectOffset;
        private Quaternion _ringRotation;
        private float _ringRadius;
        private float _ringWidth;
        private int _ringSegmentCount;
        private float _lightIntensity;
        private float _audioVolume;

        private static readonly Color Blue = new Color(0.12f, 0.62f, 1.00f, 1.0f);
        private static readonly Color Amber = new Color(1.00f, 0.52f, 0.08f, 1.0f);
        private static readonly Color Red = new Color(1.00f, 0.12f, 0.06f, 1.0f);

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
            Vector3 ringEulerAngles,
            bool enablePerHexEmission,
            float perHexGlowRadius,
            float perHexGlowWidth,
            int perHexMaximumEmitters,
            bool perHexConvergenceLines)
        {
            _effectsEnabled = effectsEnabled;
            _effectOffset = effectOffset;
            _ringRadius = Mathf.Max(0.1f, ringRadius);
            _ringWidth = Mathf.Max(0.005f, ringWidth);
            _ringSegmentCount = Mathf.Clamp(ringSegments, 8, MaximumSegments);
            _ringRotation = Quaternion.Euler(ringEulerAngles);
            _lightIntensity = Mathf.Max(0.0f, lightIntensity);
            _audioVolume = Mathf.Clamp01(audioVolume);
            _enablePerHexEmission = enablePerHexEmission;
            _perHexGlowRadius = Mathf.Max(0.02f, perHexGlowRadius);
            _perHexGlowWidth = Mathf.Max(0.004f, perHexGlowWidth);
            _perHexMaximumEmitters = Mathf.Clamp(perHexMaximumEmitters, 1, 96);
            _perHexConvergenceLines = perHexConvergenceLines;

            CreateMaterial();
            CreateStatusLight(lightRange);
            CreateAudioSources(activationClipPath, transmissionClipPath);
            CreateEnergySegments();
            CreatePulseRings();

            _stateChangedAt = Time.time;
            _initialized = true;
            HideAllGeometry();
        }

        public void SetVisualState(
            RelayVisualState state,
            float synchronizationFraction,
            bool stateChanged)
        {
            if (!_initialized)
                return;

            _syncFraction = Mathf.Clamp01(synchronizationFraction);

            if (!stateChanged && state == _state)
                return;

            RelayVisualState previous = _state;
            _state = state;
            _stateChangedAt = Time.time;

            if (_state == RelayVisualState.Entangled &&
                previous != RelayVisualState.Entangled)
            {
                PlayOneShot(_activationSource);
            }
        }

        public void TriggerTransmissionPulse()
        {
            if (!_initialized ||
                _state != RelayVisualState.Entangled ||
                Time.time < _nextTransmissionAllowed)
            {
                return;
            }

            _nextTransmissionAllowed = Time.time + 0.75f;
            _transmissionUntil = Time.time + 0.65f;
            PlayOneShot(_transmissionSource);
        }

        private void Update()
        {
            if (!_initialized)
                return;

            if (!_effectsEnabled)
            {
                HideAllGeometry();
                if (_statusLight != null)
                    _statusLight.enabled = false;
                return;
            }

            float elapsed = Mathf.Max(0.0f, Time.time - _stateChangedAt);
            bool transmitting = Time.time < _transmissionUntil;

            RenderStatusLight(elapsed, transmitting);
            RenderEnergyRing(elapsed, transmitting);
            RenderRadiatingPulses(elapsed, transmitting);
            RenderPerHexEmitters(elapsed, transmitting);
        }

        private void RenderStatusLight(float elapsed, bool transmitting)
        {
            if (_statusLight == null)
                return;

            float intensity = 0.0f;
            Color color = Blue;

            switch (_state)
            {
                case RelayVisualState.Standby:
                    color = Amber;
                    intensity = _lightIntensity * (0.06f + 0.18f * SharpPulse(elapsed, 2.0f));
                    break;

                case RelayVisualState.Initializing:
                    intensity = _lightIntensity * (0.25f + 0.45f * SmoothPulse(elapsed, 1.0f));
                    break;

                case RelayVisualState.Entangled:
                    intensity = _lightIntensity * (0.40f + 0.20f * SmoothPulse(elapsed, 3.2f));
                    break;

                case RelayVisualState.Transmitting:
                    intensity = _lightIntensity * 1.7f;
                    break;

                case RelayVisualState.Fault:
                    color = Red;
                    intensity = _lightIntensity * (0.05f + 0.30f * SharpPulse(elapsed, 2.5f));
                    break;
            }

            if (transmitting)
                intensity = Mathf.Max(intensity, _lightIntensity * 1.8f);

            _statusLight.color = color;
            _statusLight.intensity = Mathf.Max(0.0f, intensity);
            _statusLight.enabled = intensity > 0.005f;
        }

        private void RenderEnergyRing(float elapsed, bool transmitting)
        {
            if (_segments == null)
                return;

            if (_state == RelayVisualState.Folded ||
                _state == RelayVisualState.Standby ||
                _state == RelayVisualState.Fault)
            {
                SetSegmentsVisible(false);
                return;
            }

            float turnsPerSecond = _state == RelayVisualState.Initializing ? 0.22f : 0.10f;
            if (transmitting)
                turnsPerSecond = 0.75f;

            float head = Mathf.Repeat(elapsed * turnsPerSecond, 1.0f);
            float illuminatedFraction = _state == RelayVisualState.Initializing
                ? Mathf.Clamp01(Mathf.Max(0.08f, _syncFraction))
                : 1.0f;

            float breathing = _state == RelayVisualState.Entangled
                ? 0.78f + 0.22f * SmoothPulse(elapsed, 3.2f)
                : 0.85f;

            for (int i = 0; i < _segments.Length; i++)
            {
                LineRenderer segment = _segments[i];
                if (segment == null)
                    continue;

                float normalized = (float)i / _segments.Length;
                float clockwiseDistance = Mathf.Repeat(head - normalized, 1.0f);
                bool lit = clockwiseDistance <= illuminatedFraction;

                if (!lit)
                {
                    segment.enabled = false;
                    continue;
                }

                float trailingFade = illuminatedFraction >= 0.999f
                    ? 0.55f + 0.45f * (1.0f - clockwiseDistance)
                    : Mathf.Clamp01(1.0f - clockwiseDistance / Mathf.Max(0.05f, illuminatedFraction));

                float alpha = Mathf.Clamp01((0.18f + 0.82f * trailingFade) * breathing);
                if (transmitting)
                {
                    float pulsePosition = Mathf.Repeat(head * 1.8f, 1.0f);
                    float pulseDistance = CircularDistance(normalized, pulsePosition);
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(1.0f - pulseDistance * 15.0f));
                }

                Color color = new Color(Blue.r, Blue.g, Blue.b, alpha);
                segment.startColor = color;
                segment.endColor = color;
                segment.startWidth = _ringWidth * (transmitting ? 1.5f : 1.0f);
                segment.endWidth = segment.startWidth;
                segment.enabled = alpha > 0.015f;
            }
        }

        private void RenderRadiatingPulses(float elapsed, bool transmitting)
        {
            if (_pulseRings == null)
                return;

            bool active = _state == RelayVisualState.Initializing ||
                          _state == RelayVisualState.Entangled;

            if (!active)
            {
                SetPulseRingsVisible(false);
                return;
            }

            float cycle = transmitting ? 0.55f : (_state == RelayVisualState.Initializing ? 1.35f : 2.8f);
            float strength = transmitting ? 1.0f : (_state == RelayVisualState.Initializing ? 0.58f : 0.28f);

            for (int i = 0; i < _pulseRings.Length; i++)
            {
                LineRenderer ring = _pulseRings[i];
                if (ring == null)
                    continue;

                float phase = Mathf.Repeat(elapsed / cycle + (float)i / _pulseRings.Length, 1.0f);
                float radius = _ringRadius * (1.0f + phase * (transmitting ? 0.85f : 0.45f));
                float alpha = Mathf.Clamp01((1.0f - phase) * strength);

                UpdateCircle(ring, radius);
                Color color = new Color(Blue.r, Blue.g, Blue.b, alpha);
                ring.startColor = color;
                ring.endColor = color;
                ring.startWidth = _ringWidth * Mathf.Lerp(1.15f, 0.35f, phase);
                ring.endWidth = ring.startWidth;
                ring.enabled = alpha > 0.015f;
            }
        }

        private void RenderPerHexEmitters(float elapsed, bool transmitting)
        {
            if (!_enablePerHexEmission)
            {
                SetPerHexVisible(false);
                return;
            }

            bool active = _state == RelayVisualState.Initializing ||
                          _state == RelayVisualState.Entangled;

            if (!active)
            {
                SetPerHexVisible(false);
                return;
            }

            if (_perHexEmitters.Count == 0 && Time.time >= _nextPanelDiscoveryAt)
            {
                _nextPanelDiscoveryAt = Time.time + 2.0f;
                DiscoverReflectorPanels();
            }

            float stateStrength = _state == RelayVisualState.Initializing
                ? Mathf.Lerp(0.25f, 0.85f, _syncFraction)
                : 0.62f;
            float pulse = 0.70f + 0.30f * SmoothPulse(elapsed, transmitting ? 0.45f : 2.1f);
            float alpha = Mathf.Clamp01(stateStrength * pulse * (transmitting ? 1.55f : 1.0f));
            float radiusScale = transmitting ? 1.45f : 1.0f;

            for (int i = 0; i < _perHexEmitters.Count; i++)
            {
                PerHexEmitter emitter = _perHexEmitters[i];
                if (emitter == null)
                    continue;

                float stagger = Mathf.Repeat(elapsed * (transmitting ? 2.2f : 0.55f) + i * 0.083f, 1.0f);
                float localPulse = 0.72f + 0.28f * Mathf.Sin(stagger * Mathf.PI * 2.0f);
                float emitterAlpha = Mathf.Clamp01(alpha * localPulse);

                UpdatePerHexCircle(emitter.Ring, emitter.Center, emitter.AxisA, emitter.AxisB,
                    _perHexGlowRadius * radiusScale * (0.88f + 0.18f * localPulse));

                Color color = new Color(Blue.r, Blue.g, Blue.b, emitterAlpha);
                emitter.Ring.startColor = color;
                emitter.Ring.endColor = color;
                emitter.Ring.startWidth = _perHexGlowWidth * (transmitting ? 1.6f : 1.0f);
                emitter.Ring.endWidth = emitter.Ring.startWidth;
                emitter.Ring.enabled = emitterAlpha > 0.02f;

                if (emitter.Core != null)
                {
                    UpdatePerHexCircle(emitter.Core, emitter.Center, emitter.AxisA, emitter.AxisB,
                        _perHexGlowRadius * 0.18f * (transmitting ? 1.4f : 1.0f));
                    emitter.Core.startColor = color;
                    emitter.Core.endColor = color;
                    emitter.Core.startWidth = _perHexGlowWidth * 1.8f;
                    emitter.Core.endWidth = emitter.Core.startWidth;
                    emitter.Core.enabled = emitterAlpha > 0.02f;
                }

                if (emitter.FocusLine != null)
                {
                    emitter.FocusLine.positionCount = 2;
                    emitter.FocusLine.SetPosition(0, emitter.Center);
                    emitter.FocusLine.SetPosition(1, _effectOffset);
                    Color faint = new Color(Blue.r, Blue.g, Blue.b,
                        emitterAlpha * (transmitting ? 0.75f : 0.16f));
                    emitter.FocusLine.startColor = faint;
                    emitter.FocusLine.endColor = new Color(Blue.r, Blue.g, Blue.b, 0.0f);
                    emitter.FocusLine.startWidth = _perHexGlowWidth * (transmitting ? 0.85f : 0.35f);
                    emitter.FocusLine.endWidth = _perHexGlowWidth * 0.15f;
                    emitter.FocusLine.enabled = transmitting || emitterAlpha > 0.12f;
                }
            }
        }

        private void DiscoverReflectorPanels()
        {
            Part owningPart = GetComponentInParent<Part>();
            Renderer[] renderers = owningPart != null
                ? owningPart.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];

            List<PanelCandidate> candidates = new List<PanelCandidate>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is LineRenderer || !renderer.gameObject.activeInHierarchy)
                    continue;

                PanelCandidate candidate;
                if (TryCreatePanelCandidate(renderer, out candidate))
                    candidates.Add(candidate);
            }

            if (candidates.Count < 3)
                return;

            // Reflector petals are normally a repeated set of similarly-sized,
            // very flat meshes. Select the largest repeated size group instead
            // of relying on model-specific transform names.
            Dictionary<string, List<PanelCandidate>> groups = new Dictionary<string, List<PanelCandidate>>();
            for (int i = 0; i < candidates.Count; i++)
            {
                PanelCandidate candidate = candidates[i];
                string key = QuantizedSizeKey(candidate.SortedDimensions);
                List<PanelCandidate> group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new List<PanelCandidate>();
                    groups.Add(key, group);
                }
                group.Add(candidate);
            }

            List<PanelCandidate> selected = null;
            float selectedScore = 0.0f;
            foreach (KeyValuePair<string, List<PanelCandidate>> pair in groups)
            {
                List<PanelCandidate> group = pair.Value;
                if (group.Count < 3)
                    continue;

                float score = group.Count * group[0].FaceArea;
                if (score > selectedScore)
                {
                    selected = group;
                    selectedScore = score;
                }
            }

            if (selected == null)
                return;

            selected.Sort(delegate(PanelCandidate a, PanelCandidate b)
            {
                return Vector3.Distance(a.Center, _effectOffset)
                    .CompareTo(Vector3.Distance(b.Center, _effectOffset));
            });

            int count = Mathf.Min(selected.Count, _perHexMaximumEmitters);
            for (int i = 0; i < count; i++)
                CreatePerHexEmitter(selected[i], i);

            Debug.Log("[QuantumRelay] Per-panel visual emission attached to " + count + " reflector panels.");
        }

        private bool TryCreatePanelCandidate(Renderer renderer, out PanelCandidate candidate)
        {
            candidate = new PanelCandidate();

            Bounds localBounds;
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (meshFilter != null && meshFilter.sharedMesh != null)
                localBounds = meshFilter.sharedMesh.bounds;
            else if (skinned != null)
                localBounds = skinned.localBounds;
            else
                return false;

            Vector3 size = localBounds.size;
            float x = Mathf.Abs(size.x);
            float y = Mathf.Abs(size.y);
            float z = Mathf.Abs(size.z);
            float smallest = Mathf.Min(x, Mathf.Min(y, z));
            float largest = Mathf.Max(x, Mathf.Max(y, z));
            float middle = x + y + z - smallest - largest;

            if (largest < 0.15f || middle < 0.12f)
                return false;
            if (smallest / Mathf.Max(0.001f, middle) > 0.28f)
                return false;
            if (largest / Mathf.Max(0.001f, middle) > 2.2f)
                return false;

            Vector3 localNormal;
            Vector3 localAxisA;
            Vector3 localAxisB;
            if (x <= y && x <= z)
            {
                localNormal = Vector3.right;
                localAxisA = Vector3.up;
                localAxisB = Vector3.forward;
            }
            else if (y <= x && y <= z)
            {
                localNormal = Vector3.up;
                localAxisA = Vector3.right;
                localAxisB = Vector3.forward;
            }
            else
            {
                localNormal = Vector3.forward;
                localAxisA = Vector3.right;
                localAxisB = Vector3.up;
            }

            Transform panelTransform = renderer.transform;
            Vector3 worldCenter = panelTransform.TransformPoint(localBounds.center);
            Vector3 worldAxisA = panelTransform.TransformDirection(localAxisA).normalized;
            Vector3 worldAxisB = panelTransform.TransformDirection(localAxisB).normalized;

            candidate.Center = transform.InverseTransformPoint(worldCenter);
            candidate.AxisA = transform.InverseTransformDirection(worldAxisA).normalized;
            candidate.AxisB = transform.InverseTransformDirection(worldAxisB).normalized;
            candidate.Normal = transform.InverseTransformDirection(
                panelTransform.TransformDirection(localNormal)).normalized;
            candidate.SortedDimensions = new Vector3(smallest, middle, largest);
            candidate.FaceArea = middle * largest;
            return true;
        }

        private void CreatePerHexEmitter(PanelCandidate panel, int index)
        {
            GameObject owner = new GameObject("QuantumPerHexEmitter_" + index);
            owner.transform.SetParent(transform, false);

            LineRenderer ring = ConfigureLineRenderer(owner, true);
            ring.positionCount = PerHexRingPoints;
            ring.enabled = false;

            GameObject coreOwner = new GameObject("QuantumPerHexCore_" + index);
            coreOwner.transform.SetParent(transform, false);
            LineRenderer core = ConfigureLineRenderer(coreOwner, true);
            core.positionCount = PerHexRingPoints;
            core.enabled = false;

            LineRenderer focusLine = null;
            if (_perHexConvergenceLines)
            {
                GameObject lineOwner = new GameObject("QuantumPerHexFocus_" + index);
                lineOwner.transform.SetParent(transform, false);
                focusLine = ConfigureLineRenderer(lineOwner, false);
                focusLine.positionCount = 2;
                focusLine.enabled = false;
            }

            _perHexEmitters.Add(new PerHexEmitter
            {
                Center = panel.Center + panel.Normal * 0.015f,
                AxisA = panel.AxisA,
                AxisB = panel.AxisB,
                Ring = ring,
                Core = core,
                FocusLine = focusLine
            });
        }

        private static string QuantizedSizeKey(Vector3 dimensions)
        {
            return Mathf.RoundToInt(dimensions.x * 20.0f) + ":" +
                   Mathf.RoundToInt(dimensions.y * 20.0f) + ":" +
                   Mathf.RoundToInt(dimensions.z * 20.0f);
        }

        private static void UpdatePerHexCircle(
            LineRenderer line,
            Vector3 center,
            Vector3 axisA,
            Vector3 axisB,
            float radius)
        {
            if (line == null)
                return;

            line.positionCount = PerHexRingPoints;
            for (int i = 0; i < PerHexRingPoints; i++)
            {
                float angle = ((float)i / PerHexRingPoints) * Mathf.PI * 2.0f;
                line.SetPosition(i, center + axisA * Mathf.Cos(angle) * radius +
                    axisB * Mathf.Sin(angle) * radius);
            }
        }

        private void SetPerHexVisible(bool visible)
        {
            for (int i = 0; i < _perHexEmitters.Count; i++)
            {
                PerHexEmitter emitter = _perHexEmitters[i];
                if (emitter == null)
                    continue;
                if (emitter.Ring != null)
                    emitter.Ring.enabled = visible;
                if (emitter.Core != null)
                    emitter.Core.enabled = visible;
                if (emitter.FocusLine != null)
                    emitter.FocusLine.enabled = visible;
            }
        }

        private sealed class PerHexEmitter
        {
            public Vector3 Center;
            public Vector3 AxisA;
            public Vector3 AxisB;
            public LineRenderer Ring;
            public LineRenderer Core;
            public LineRenderer FocusLine;
        }

        private struct PanelCandidate
        {
            public Vector3 Center;
            public Vector3 AxisA;
            public Vector3 AxisB;
            public Vector3 Normal;
            public Vector3 SortedDimensions;
            public float FaceArea;
        }

        private void CreateMaterial()
        {
            Shader shader = Shader.Find("Particles/Additive");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader != null)
            {
                _lineMaterial = new Material(shader);
                _lineMaterial.name = "QuantumRelayEnergyMaterial";
            }
        }

        private void CreateStatusLight(float lightRange)
        {
            GameObject lightObject = new GameObject("QuantumRelayStatusLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = _effectOffset;

            _statusLight = lightObject.AddComponent<Light>();
            _statusLight.type = LightType.Point;
            _statusLight.range = Mathf.Max(0.1f, lightRange);
            _statusLight.shadows = LightShadows.None;
            _statusLight.renderMode = LightRenderMode.ForcePixel;
            _statusLight.enabled = false;
        }

        private void CreateAudioSources(string activationClipPath, string transmissionClipPath)
        {
            _activationSource = CreateAudioSource("QuantumRelayActivationAudio", activationClipPath);
            _transmissionSource = CreateAudioSource("QuantumRelayTransmissionAudio", transmissionClipPath);
        }

        private AudioSource CreateAudioSource(string objectName, string clipPath)
        {
            GameObject audioObject = new GameObject(objectName);
            audioObject.transform.SetParent(transform, false);
            audioObject.transform.localPosition = _effectOffset;

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

        private void CreateEnergySegments()
        {
            _segments = new LineRenderer[_ringSegmentCount];
            float gapFraction = 0.22f;

            for (int i = 0; i < _segments.Length; i++)
            {
                GameObject segmentObject = new GameObject("QuantumEnergySegment_" + i);
                segmentObject.transform.SetParent(transform, false);
                LineRenderer line = ConfigureLineRenderer(segmentObject, false);

                float start = ((float)i / _segments.Length) * Mathf.PI * 2.0f;
                float end = ((i + 1.0f - gapFraction) / _segments.Length) * Mathf.PI * 2.0f;
                line.positionCount = 2;
                line.SetPosition(0, RingPoint(start, _ringRadius));
                line.SetPosition(1, RingPoint(end, _ringRadius));
                line.enabled = false;
                _segments[i] = line;
            }
        }

        private void CreatePulseRings()
        {
            _pulseRings = new LineRenderer[PulseRingCount];
            for (int i = 0; i < _pulseRings.Length; i++)
            {
                GameObject ringObject = new GameObject("QuantumRadiatingPulse_" + i);
                ringObject.transform.SetParent(transform, false);
                LineRenderer line = ConfigureLineRenderer(ringObject, true);
                UpdateCircle(line, _ringRadius);
                line.enabled = false;
                _pulseRings[i] = line;
            }
        }

        private LineRenderer ConfigureLineRenderer(GameObject owner, bool loop)
        {
            LineRenderer line = owner.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.alignment = LineAlignment.TransformZ;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.startWidth = _ringWidth;
            line.endWidth = _ringWidth;
            if (_lineMaterial != null)
                line.material = _lineMaterial;
            return line;
        }

        private void UpdateCircle(LineRenderer line, float radius)
        {
            if (line == null)
                return;

            int points = Mathf.Max(24, _ringSegmentCount * 2);
            line.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float angle = ((float)i / points) * Mathf.PI * 2.0f;
                line.SetPosition(i, RingPoint(angle, radius));
            }
        }

        private Vector3 RingPoint(float angle, float radius)
        {
            Vector3 point = new Vector3(
                Mathf.Cos(angle) * radius,
                0.0f,
                Mathf.Sin(angle) * radius);
            return _effectOffset + _ringRotation * point;
        }

        private static float CircularDistance(float a, float b)
        {
            float distance = Mathf.Abs(a - b);
            return Mathf.Min(distance, 1.0f - distance);
        }

        private static float SmoothPulse(float elapsed, float period)
        {
            float safePeriod = Mathf.Max(0.1f, period);
            return 0.5f + 0.5f * Mathf.Sin((elapsed / safePeriod) * Mathf.PI * 2.0f);
        }

        private static float SharpPulse(float elapsed, float period)
        {
            float pulse = SmoothPulse(elapsed, period);
            return pulse * pulse * pulse * pulse;
        }

        private void PlayOneShot(AudioSource source)
        {
            if (!_effectsEnabled || source == null || source.clip == null)
                return;

            source.Stop();
            source.Play();
        }

        private void SetSegmentsVisible(bool visible)
        {
            if (_segments == null)
                return;

            for (int i = 0; i < _segments.Length; i++)
            {
                if (_segments[i] != null)
                    _segments[i].enabled = visible;
            }
        }

        private void SetPulseRingsVisible(bool visible)
        {
            if (_pulseRings == null)
                return;

            for (int i = 0; i < _pulseRings.Length; i++)
            {
                if (_pulseRings[i] != null)
                    _pulseRings[i].enabled = visible;
            }
        }

        private void HideAllGeometry()
        {
            SetSegmentsVisible(false);
            SetPulseRingsVisible(false);
            SetPerHexVisible(false);
        }

        private void OnDestroy()
        {
            if (_activationSource != null)
                _activationSource.Stop();
            if (_transmissionSource != null)
                _transmissionSource.Stop();
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }
    }
}
