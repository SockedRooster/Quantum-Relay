# Changelog

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
