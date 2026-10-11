# Quantum Relay — Development Roadmap

This document tracks **planned** features, not functionality in released builds. Milestones are targets; scope and versions may be adjusted after testing.

## Near term — Stabilization before v1.5

- Restore all relay interactions in the VAB and verify functional buttons.
- Restore persistence for the user's resized Quantum Relay menu across scene changes and save reloads.
- Regression-test simultaneous quantum links, established wormhole networks, save/load behavior and CommNet integration before adding new science modules.
- Validate recent UI and network diagnostics against existing saved games.

## v1.5 — QSA-100 Quantum Survey Array (planned)

**Single new physical part:** **QSA-100 Quantum Survey Array**, a compact probe/station-mounted science instrument integrated directly into the existing **Quantum Relay** plugin and repository. No separate Surveyor addon or extra dependency is required.

### Gameplay goals

1. **Wormhole discovery:** Detect and select valid nearby quantum anomalies/wormhole endpoints using the existing Quantum Relay wormhole-discovery subsystem.
2. **Initial survey:** Characterize the endpoint's field signature, approximate stability and signal coherence, recording a first-stage discovery.
3. **Deep survey:** At a closer configurable approach distance, complete higher-resolution characterization and resolve available destination information. Both stages use the **same QSA-100 part**.
4. **Science experiments:** Generate finite, meaningful research science from the instrument. Support **Kerbalism** experiments/science processing where Kerbalism is installed, with **stock KSP science fallback** when it is not.
5. **Persistent discoveries:** Save discoveries per stable wormhole endpoint identifier and survey stage in game-specific Quantum Relay scenario data, and show them in the existing network interface.
6. **Multiple wormholes:** Independently record and display multiple simultaneous surveyed endpoints/links; completing one survey must not overwrite another.

### Technical/design requirements

- Integrate with the existing `WormholeScanner`, relay registry/network UI and `QuantumRelayScenario` state rather than introducing a second discovery database.
- Add **one** installable part with a distinct internal part ID, using appropriate ElectricCharge cost and conditions for vessel proximity/operation.
- Do not invent random stability/signal percentages as game facts. Define reproducible, testable measurements from actual available wormhole and quantum-link state; mark unsupported properties **unknown**.
- Limit repeated science: track experiment/survey completion by stable endpoint identity and stage; avoid indefinite science farming across scene changes, quickloads or cloned craft.
- Kerbalism support must be implemented through validated Kerbalism-compatible science configuration and runtime behavior, not by assuming stock `ModuleScienceExperiment` automatically behaves the same.
- The instrument should operate without Kerbalism via a tested stock-science path; no mandatory Kerbalism dependency for the base Quantum Relay addon.
- Preserve established relay networks and existing saves. Surveys enhance discovery/science and optionally inform diagnostics; **they do not gate existing wormhole travel or communications**.
- Provide clear user controls and status in the part action window and/or existing QR menu: target, proximity, survey stage, progress, science availability and already-completed state.
- Design for unloaded/loaded vessel and save/load transitions: do not award science or duplicate surveys because of scene changes.
- Keep compatibility with configured wormhole body endpoints, including multiple star systems and modded planetary packs, without hardcoded names.

### Proposed v1.5 acceptance tests

- A craft with **one QSA-100** completes initial and deep surveys at a supported wormhole.
- Surveys persist after scene changes, quicksave/reload and returning to the tracking station.
- Surveying two separate wormholes retains both independent records and produces no cross-overwriting.
- Repeating a completed stage does **not** generate unlimited science.
- Kerbalism-enabled and stock-science-only installs both award/handle finite science correctly.
- Existing QR links and relay networks still work with no QSA-100 parts installed.
- No VAB relay-control regressions; the QR window continues remembering its configured size.

### Out of scope for v1.5

- Multiple surveyor part sizes or dedicated observatory variants.
- Mandatory survey permissions to travel through a wormhole or establish quantum communications.
- Standalone `RoosterWorks-Deep-Space-Surveyor` plugin/release.
- Unverified physics claims or gameplay values presented as real telemetry.

**Implementation status: Planned / not yet coded.** No v1.5 binary, part model or science configuration is included by adding this roadmap.
