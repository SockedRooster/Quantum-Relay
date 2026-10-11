# Quantum Relay v1.02

KSP 1.12.x quantum CommNet relay mod. This repository includes the full C# source project in `QuantumRelay/` and the complete installation tree under `GameData/QuantumRelay/`.

## Build

Open `QuantumRelay/QuantumRelay.csproj` in a Windows development environment with the KSP managed assemblies available. Rebuild in Release configuration using MSBuild. Copy the compiled `QuantumRelay.dll` into `GameData/QuantumRelay/Plugins/`.

The included DLL is the maintainer-tested v1.02 binary. The archive has been repackaged, not recompiled.

## Install

Copy `GameData/QuantumRelay` to your KSP `GameData` folder after backing up your prior installation.

## Known issue

The VAB relay buttons are nonfunctional; a fix is planned for v1.03.

## Development roadmap

See [ROADMAP.md](ROADMAP.md) for planned work, including the **v1.15 QSA-100 Quantum Survey Array**: one wormhole survey part with optional Kerbalism science integration, stock-science fallback, and persistent discoveries.

## Project

https://github.com/SockedRooster/Quantum-Relay
