# Quantum Relay v1.03

> Packaging erratum: the published v1.03 GameData ZIP was confirmed to contain
> legacy NFE patches instead of the standalone RoosterWorks parts. Its SHA256
> matches the affected local artifact. Use the complete v1.03.1 correction;
> see RELEASE_NOTES_v1.03.1.md. The earlier in-game validation refers to the
> development builds, not independent testing of the incorrect public package.

Release of the stabilization, HUD, network-map, and menu work tested through
v1.03V. KSP 1.12.x; optional Kerbalism integration.

## Changes since v1.02

- VAB standby/synchronization/operational preview automatically refreshes the
  Kerbalism planner. Custom controls remain; redundant converter PAW controls
  are hidden. Editor preview processes no longer charge EC in flight.
- Cyan QUANTUM LINK badge appears only for a verified quantum hop in the active
  craft's selected CommNet control route. Settings control visibility and position.
- Network Map shows online/offline gateway pairs and highlights the active
  craft's quantum route. Gateway details and full-name tooltips are available.
- Diagnostics includes all configured networks. Status and gateway checklists
  expand on demand; the map adapts to narrow windows.
- Settings/menu layout persist; menu scale supports 75–150%. Automatic rebuild
  and detailed gateway logging preferences are honored. Failed saves are reported.
- Unloaded gateway lookup uses current orbital data. Changed CommNet node
  references request a rebuild even if the gateway vessel IDs are unchanged.
- Established Debdeb/Tuun network definitions and relay power ownership remain.

## Validation and limits

The maintainer reported all in-game acceptance checks passed through v1.03V,
including the final fresh-load and UI regression checklist. This is maintainer
confirmation; the packaging agent did not launch or independently test KSP.
The release changes metadata/documentation/build asset staging, not gameplay.

Release build against installed KSP assemblies: zero warnings/errors.
Automated suite: 69 contract checks (15 settings, 19 startup, 28 route,
7 settings-node). Stand-ins exercise production methods; they do not reproduce
Unity UI or KSP's CFG parser. Release archives are checked against staged files.

Public version: v1.03; numeric release/file metadata: 1.3.0/1.3.0.0. Assembly
binding identity remains 1.2.0.0 and KSPAssembly 1,2 for compatibility.

## Installation

Exit KSP. Extract the player ZIP so GameData/QuantumRelay is inside the game.
It contains no Settings.cfg, so existing personal preferences are preserved.
For a new install, default settings are created at runtime. When upgrading an
existing v1.03 development build, replacing only Plugins/QuantumRelay.dll is
sufficient. Keep customized PluginData/QuantumBridges.cfg; the full player ZIP
contains the standard Debdeb/Tuun bridge definitions. Avoid duplicate DLLs.

The QR hardware patches require Near Future Exploration and ModuleManager.
The player ZIP includes QR assets/configurations, not those dependencies.
The source ZIP includes the reproducible source/distribution tree and tests.

v1.15 Quantum Survey Array remains planned and is not part of this release.
