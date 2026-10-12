# Quantum Relay

**Release:** v1.03 · **Author:** SockedRooster · **License:** MIT
**Target:** Kerbal Space Program 1.12.x

Quantum Relay provides quantum CommNet relay networks and wormhole connectivity for KSP 1. This repository contains the **C# source project and part-patch sources**; the installation files are supplied in the [GitHub Releases](https://github.com/SockedRooster/Quantum-Relay/releases), not in a committed `GameData/` directory.

## For players

Download the **KSP installation ZIP** attached to the intended GitHub release. Install the `QuantumRelay` game folder under `<KSP>/GameData/`, so the plugin is at `<KSP>/GameData/QuantumRelay/Plugins/QuantumRelay.dll`. Back up saves before changing versions.

The four QR-100/250/500/750 relay hardware configurations under `QuantumRelay/Parts/` are ModuleManager patches of **Near Future Exploration** reflector parts; using those hardware parts requires Near Future Exploration and ModuleManager. The plugin is separate from those part assets.

## For developers

- `QuantumRelay/` — C# .NET Framework 4.7.2 project, source code, part configs and audio.
- `QuantumRelay/QuantumRelay.csproj` — compile inputs and asset staging.
- `QuantumRelay/ConceptArt/` — concept artwork (not an in-game texture).
- `Distribution/QuantumRelay/` — versioned runtime assets/configs staged by the build; excludes generated DLLs and personal settings.
- `Tests/` — 69 contract checks with stand-in KSP types.
- `RELEASE_NOTES_v1.03.md` — release changes, maintainer validation, and installation notes.
- `ROADMAP.md` — future features and stabilization tasks.

Open `QuantumRelay/QuantumRelay.csproj` in a Windows .NET/MSBuild environment with KSP's managed assemblies available. Build in Release configuration. Set the `KSPRoot` MSBuild property to your local game directory if the project default differs, for example:

```powershell
msbuild QuantumRelay/QuantumRelay.csproj /p:Configuration=Release /p:KSPRoot="D:\\Games\\Kerbal Space Program"
```

The build now stages files **inside this repository**, in a generated `GameData/QuantumRelay/` directory:

```text
GameData/
└── QuantumRelay/
    ├── Plugins/QuantumRelay.dll
    ├── Parts/QR-100_Pioneer.cfg (and three other QR-series configs)
    ├── Sounds/Activation.wav
    ├── Sounds/TransmissionPulse.ogg
    └── LICENSE
```

Package `GameData/` as the top-level folder of a **player ZIP** when preparing a tested release. Generated `GameData/QuantumRelay`, compiler `obj/` and `bin/` directories are ignored by Git. Do not use the entire source repository as a player installation.

`Package-Release.ps1` builds the plugin, creates player/source ZIPs and SHA256SUMS.txt in `artifacts/`, and verifies every archive entry against its input file. The source ZIP also includes the compiled GameData tree. Use `-KSPRoot` for a different game location and `-NoRestore` only after restoring the build project. Run the three projects under `Tests/` separately for the .NET 10 contract checks.

**Versioning:** Public v1.03 uses numeric release/file metadata 1.3.0/1.3.0.0. Assembly binding identity stays 1.2.0.0 and KSPAssembly 1,2 for compatibility. The display version and release metadata now agree. Runtime Settings.cfg is created automatically and excluded from player packages.

## Validation and next milestone

- VAB power preview, fresh-load connections, simultaneous Debdeb/Tuun links, HUD/map route highlighting, settings persistence, and compact menus were reported passing by the maintainer through v1.03V.
- Release build and 69 automated contract checks passed. These automated fixtures do not run KSP or Unity; see the release notes for the distinction.
- See [ROADMAP.md](ROADMAP.md) for the proposed v1.15 Quantum Survey Array and other plans; roadmap features are **not** included in the current release.

Original RoosterWorks project, MIT licensed. [Source and issue tracker](https://github.com/SockedRooster/Quantum-Relay).
