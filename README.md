# Quantum Relay — Sprint 2, Task 2.5

## Live relay power telemetry

The QR-100 correctly consumes its configured ElectricCharge, and the individual
gateway card already receives its live relay power rate. However, the bridge
header and diagnostics monitor still display the legacy global power setting.

This patch changes those displays to use the actual selected gateway telemetry.

## Changes

### Bridge header

Replaces the static global estimate with:

```text
Live relay draw: 15 EC/s total
Gateway A: 7.5 EC/s | Gateway B: 7.5 EC/s
```

The displayed values follow each relay's current state. A disabled, retracted,
or otherwise idle relay can therefore show a lower draw than an operational
relay.

### Diagnostics

Adds:

```text
Configured fallback power
Gateway A live draw
Gateway B live draw
Combined live draw
```

The fallback value is retained for legacy reflector compatibility.

### Version

Updates the interface label from `v1.2 alpha 2` to `v1.2 alpha 3`.

## Apply

Extract the ZIP. Open PowerShell in the repository root and run:

```powershell
& "FULL\PATH\TO\QuantumRelay_Sprint2_Task2.5_LivePowerTelemetry\Apply-Task2.5.ps1"
```

Then build:

```powershell
dotnet build
```

Replace the installed DLL:

```text
GameData\QuantumRelay\Plugins\QuantumRelay.dll
```

## Test

1. Load a flight containing the QR-100.
2. Open Quantum Relay.
3. Confirm the bridge header displays the QR-100's actual live draw.
4. Open Diagnostics.
5. Confirm Gateway A/B and combined draw values are shown.
6. Retract or disable the relay.
7. Confirm the displayed live draw changes with the relay state.
8. Confirm the vessel battery continues dropping while the relay is drawing EC.

## Suggested commit

```text
fix: display live relay EC draw in systems monitor
```
