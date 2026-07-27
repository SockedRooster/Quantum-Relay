# Changelog

## 1.6.0 Preview 1 — Interface Polish

- Added collapsible live and saved network cards.
- Added Expand All and Collapse All controls.
- Added a network summary panel with online, offline, gateway, and power statistics.
- Improved network headings and visual grouping for faster scanning.
- Window width and height are now saved alongside its screen position.
- Corrected version metadata so the GUI, assembly, and project report the same version.

## 1.5.0 — Quantum Networks

### Added

- `QuantumManager` singleton for authoritative routing state.
- `QuantumRouter` for same-network and cross-network route descriptions.
- `QuantumRouteCache` with automatic short-lived expiry.
- Immutable `QuantumRoute` and directional `RouteKey` models.
- `QuantumRoutingService` facade for later CommNet integration.
- Explicit Debdeb and Tuun wormhole-network definitions.

### Changed

- Project, assembly, and runtime version metadata updated to 1.5.0.
- Route cache is invalidated whenever the published quantum topology changes.

### Notes

- This sprint does not inject quantum routes into stock CommNet yet.
- Cross-network routes continue to require ordinary CommNet transfer between
  the Kerbol-side entrances.

## 1.5.0 - Sprint 2A / PR1 Gateway Lifecycle

- Added a save-scoped `QuantumRelayScenario` for Flight, Space Center, and Tracking Station.
- Added explicit `QuantumManager` initialization and shutdown lifecycle.
- Added loaded relay registration and unregistration through `ModuleQuantumRelay`.
- Added route-cache invalidation when relay operational state changes.
- Added startup, shutdown, registration, and unregistration diagnostics.
- No intentional CommNet routing or gameplay behaviour changes in this PR.
