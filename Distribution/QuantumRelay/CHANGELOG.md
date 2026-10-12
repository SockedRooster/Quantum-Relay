# Quantum Relay Changelog

## v1.03.1 — Standalone parts packaging correction
- Removed the build target that overwrote standalone RoosterWorks parts with old NFE patches.
- Distribution/QuantumRelay/Parts is the single source for packaged parts.
- Added canonical part/model and archive checks, plus regression tests for incorrect assets.
- Corrected dependency and upgrade instructions. Install the full corrected parts, not only the DLL.
- Gameplay logic and standalone models/config values are unchanged.

## v1.03 — Stabilization and network console
- Automatic VAB Kerbalism planner refresh and removal of duplicate flight preview consumption.
- Cyan verified-route HUD and responsive network map with gateway details.
- Persistent settings/window layout, menu scale, and HUD visibility/position controls.
- Compact Status, expandable all-network Diagnostics, and working rebuild/logging preferences.
- Unloaded orbital gateway lookup and rebuilds after node-reference replacement.
- Maintainer reports in-game acceptance checks passed through v1.03V; 69 contract checks pass.

## v1.02 — Stable Quantum Networks
- Validated two simultaneous wormhole CommNet extensions (Debdeb/Kevba and Tuun/Borgal).
- Standardized version display and release metadata.
- Includes four relay models, associated assets, configurations and optional Kerbalism patch.
- Known issue: VAB relay buttons do not operate as intended.

Next planned milestone: v1.15 QSA-100 Quantum Survey Array, not included in this release.
