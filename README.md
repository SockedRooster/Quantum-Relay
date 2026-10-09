# Quantum Relay

### True CommNet Routing Through Wormholes

**Developed by [SockedRooster](https://github.com/SockedRooster)**  
A **RoosterWorks** project  
**Current release:** v1.7.3 RC1 · **License:** MIT · **Game:** Kerbal Space Program 1.12.5

Quantum Relay extends Kerbal Space Program's stock **CommNet** system to support communication links through the paired wormholes in **Promised Worlds**. Build gateway satellites, deploy purpose-built RoosterWorks relay dishes, and synchronize a pair of gateways to maintain communications with spacecraft operating in another solar system.

> **Release status:** v1.7.3 RC1 is a release candidate. Gateway synchronization, remote control across connected systems, and persistence after saving and reloading have been tested in-game. Full vessel transit through wormholes and broader compatibility testing remain outstanding.

## Download

**[Download the latest Quantum Relay release](https://github.com/SockedRooster/Quantum-Relay/releases/latest)**  
**[View v1.7.3 RC1 and its downloads](https://github.com/SockedRooster/Quantum-Relay/releases/tag/v1.7.3RC1)**

The v1.7.3 RC1 release provides:

- `QuantumRelay.zip` — installation package
- `Source.zip` — source-code snapshot

**For installation, download `QuantumRelay.zip`, not GitHub's automatically generated “Source code” archive.**

## Requirements

- **Kerbal Space Program 1.12.5**
- **Promised Worlds**
- **ModuleManager**
- **Stock CommNet enabled**

## Installation

1. Back up your KSP saves and any existing Quantum Relay installation.
2. Download `QuantumRelay.zip` from the [v1.7.3 RC1 release](https://github.com/SockedRooster/Quantum-Relay/releases/tag/v1.7.3RC1).
3. Extract the archive and copy its `QuantumRelay` folder into your KSP `GameData` directory.
4. Confirm that the plugin is located at:

   ```text
   Kerbal Space Program/
   └── GameData/
       └── QuantumRelay/
           └── Plugins/
               └── QuantumRelay.dll
   ```

5. If present, remove old standalone **preview** relay parts from `GameData/RoosterWorks/QuantumRelay` to avoid duplicate hardware. Do not delete unrelated RoosterWorks mods.
6. Start KSP with the required dependencies installed.

## RoosterWorks Relay Hardware

Quantum Relay includes four custom relay assemblies:

| Relay | Stack attachment size |
| --- | --- |
| **QR-100** | 0.625 m |
| **QR-250** | 1.25 m |
| **QR-500** | 2.5 m |
| **QR-750** | 2.5 m |

Each relay features a custom RoosterWorks model, eight folding dish sections, an approximately **eight-second** deploy/retract animation, moving colliders, and bottom-only stack attachment.

The new hardware uses KSP's `ModuleAnimateGeneric` deployment system, with deployment readiness integrated into Quantum Relay's gateway checks.

## How Gateway Links Work

A working gateway needs:

- A vessel within the configured operating radius of a supported wormhole endpoint
- **Deployed Quantum Relay hardware**
- Probe control and CommNet-capable equipment
- Sufficient **ElectricCharge** to operate
- A compatible, synchronized gateway at the paired endpoint

The original default gateway operating radius is **250 km**, with a baseline requirement of **5 ElectricCharge per second per active gateway**. These values may be changed by installed configuration or in-game settings.

When a gateway becomes unavailable—for example, its dish retracts, it loses sufficient power, or it leaves operating range—the quantum communication link is updated accordingly.

## In-Game Control Console

Open Quantum Relay using the **stock AppLauncher** in flight or map view. The console provides gateway status, hardware readiness checks, synchronization information, diagnostics, network controls, and persistent settings.

Quantum-link quality is adjustable from **10% to 100%** in 10% increments, with **100%** as the default.

Settings are stored at:

```text
GameData/QuantumRelay/PluginData/Settings.cfg
```

Other supported parameters, including wormhole names, gateway radius, scan intervals, and power consumption, are configurable through Quantum Relay's configuration files.

## Career and Science Modes

Quantum Relay includes a **Quantum Relay Technology** node after **Experimental Electrics**, costing **1000 Science**. ModuleManager is used for tech-tree integration.

## What's New in v1.7.3 RC1

- Four standalone RoosterWorks relay models: **QR-100, QR-250, QR-500, and QR-750**
- Eight-section folding dishes with animated collision geometry
- Updated deployment detection for `ModuleAnimateGeneric`
- Fix for the hardware fault encountered with the new relay deployment system
- Confirmed gateway synchronization with new relay satellites
- Confirmed interstellar remote spacecraft control
- Confirmed synchronization persistence across vessel switches and save/reload
- Consolidated release packaging with separate installation and source downloads

Read the [full v1.7.3 RC1 release notes](https://github.com/SockedRooster/Quantum-Relay/releases/tag/v1.7.3RC1) for more information.

## Troubleshooting and Bug Reports

Search `KSP.log` for **`[QuantumRelay]`** when diagnosing problems.

When [opening an issue](https://github.com/SockedRooster/Quantum-Relay/issues), include:

- KSP and Quantum Relay versions
- Installed dependency versions
- Steps to reproduce the problem
- Relevant excerpts from `KSP.log`
- Screenshots, if useful

## Source Code

The release page includes a `Source.zip` snapshot. The repository also contains C# project files for continued development.

The v1.7.3 RC1 installation archive includes the gameplay DLL preserved from the tested working installation. The source snapshot has **not** been independently verified to reproduce that exact binary; do not assume a rebuild is byte-for-byte identical.

## License

Copyright © 2026 **RoosterWorks**.

Quantum Relay is distributed under the **MIT License**. See [LICENSE](LICENSE).
