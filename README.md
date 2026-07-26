# Quantum Relay — Sprint 2, Task 2.3

This rebuilt package is based on the current GitHub source after Task 2.2.

It does **not** contain or use a Git patch.

## Apply from VS Code

1. Extract the ZIP anywhere, such as Downloads.
2. Open the Quantum Relay repository in VS Code.
3. Open a PowerShell terminal in the repository root.
4. Run:

```powershell
& "$env:USERPROFILE\Downloads\QuantumRelay_Sprint2_Task2.3_REBUILT\Apply-Task2.3.ps1"
```

Change the path if you extracted it somewhere else.

The script uses your current terminal folder as the repository root. It creates
a timestamped backup before changing anything.

Then build:

```powershell
dotnet build
```

## Direct-copy alternative

The two complete replacement files are under `ReplacementFiles`:

- `QuantumRelayRegistry.cs`
- `QuantumRelayMissionControl.cs`

They belong in your project's `QuantumRelay` source folder.

`QuantumRelayGui.cs` is updated by the installer because only three focused
sections and one helper section need to change.

## Task 2.3 features

- Saves relay model and tier in Mission Control telemetry
- Saves operational and deployment state
- Saves synchronization status and percentage
- Saves relay EC draw
- Displays relay data during flight
- Displays last-known relay data at Space Center and Tracking Station
- Expands Diagnostics for modern and legacy relay hardware
- Selects the best gateway using readiness, tier, synchronization and EC reserve
- Provides specific offline status messages

## Suggested commit

```text
feature: display relay hardware state in mission control
```
