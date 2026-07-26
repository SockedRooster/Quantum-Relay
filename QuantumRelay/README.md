# Quantum Relay v1.3.1

Full source cleanup and stability release.

## Included fixes

- Periodic 15-second gateway discovery now reconciles links in place instead of clearing and recreating them.
- Healthy links retain their online state when the selected gateway vessels have not changed.
- Synchronization is no longer intentionally restarted by a routine gateway inventory scan.
- Removed the obsolete global signal-quality control.
- Removed the obsolete global gateway-power control.
- Modern relay power remains owned by the converter-backed `ModuleQuantumRelay` power controller.
- The network layer no longer applies a second hidden EC charge.
- Gateway activation-radius changes request an immediate gateway refresh.
- Signal tiers are hardware-defined:
  - Legacy reflector-only gateway: 25%
  - QR-100 Pioneer: 40%
  - QR-250 Voyager: 60%
  - QR-500 Event Horizon: 100%
  - QR-750 Horizon Prime: 125% hardware rating
- Mixed links are limited by their weaker endpoint.
- GUI now shows hardware signal strength and effective bridge strength separately.

## QR-750 Horizon Prime

`Parts/QR-750_HorizonPrime.cfg` is a playable tier-four relay profile using the
Near Future Exploration giant-reflector geometry at a larger scale. It includes:

- Tier 4
- 125% hardware signal rating
- Six-wormhole design capacity metadata
- 5-second synchronization
- 0.05 EC/s standby draw
- 2.5 EC/s synchronization draw
- 6.0 EC/s operational draw
- A dedicated Horizon Prime startup sequence

The concept sheet is included in `ConceptArt/`. The package does **not** contain
a new `.mu` model or custom textures; the playable part currently reuses and
rescales the Near Future Exploration giant reflector. A true custom 3D asset can
replace the model later without changing the relay profile or core code.

## Build

```powershell
dotnet clean
dotnet build -c Release
```

The project expects `KSPRoot` to point to a Kerbal Space Program installation.
You can override it on the command line:

```powershell
dotnet build -c Release -p:KSPRoot="C:\Games\Kerbal Space Program"
```

## Required test

Leave an established link active for at least two minutes. It must remain online
through multiple 15-second scans without returning to synchronization. Then test
all hardware tiers and verify 25/40/60/100/125 hardware values and weakest-endpoint
bridge values.

## Version 1.5.0 — Quantum Networks

Version 1.5.0 introduces the first routing-engine foundation for independent
Debdeb and Tuun quantum networks.

- Adds `QuantumManager` as the authoritative routing-state owner.
- Adds immutable `QuantumRoute` and directional `RouteKey` models.
- Adds route calculation and short-lived route caching.
- Adds a routing-service facade for the future CommNet integration sprint.
- Defines Debdeb through Kevba's Anomalies A/B.
- Defines Tuun through Borgal's Anomalies A/B.

Sprint 1 does not replace or modify stock CommNet routing. It establishes a
compilable routing core that later integration work can call safely.
