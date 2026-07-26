# Quantum Relay — Sprint 2, Task 2.3.1 Stability

This package does not use a Git patch. It safely edits the current local source
and creates timestamped backups first.

## Apply

Extract the ZIP. Open a PowerShell terminal in the repository root, then run the
script using its actual extracted path.

Example:

```powershell
& "D:\Downloads\QuantumRelay_Sprint2_Task2.3.1_Stability\Apply-Task2.3.1.ps1"
```

Because the terminal is open in the repository root, the script automatically
uses the correct project location.

Then build:

```powershell
dotnet build
```

Copy the new DLL to:

```text
Kerbal Space Program\GameData\QuantumRelay\Plugins\QuantumRelay.dll
```

## Changes

### QuantumRelayGui

- Immediately disables itself in unsupported scenes, including the main menu.
- Does not subscribe to toolbar events in unsupported scenes.
- Tracks whether toolbar events were registered.
- Cleans up the toolbar button and event subscriptions safely.
- Stops drawing immediately during scene transitions.
- Logs startup and destruction by scene.

### QuantumRelayMissionControl

- Immediately disables itself outside Space Center and Tracking Station.
- Logs startup and destruction by scene.

### QuantumRelayBootstrap

- Prevents duplicate event registration.
- Prevents duplicate event removal.
- Logs flight lifecycle startup and destruction.

## Important

The uploaded KSP log points to an EVE main-menu cloud-handler exception rather
than a Quantum Relay exception. This update isolates Quantum Relay from the
main-menu lifecycle, but it may not eliminate a grey screen caused inside EVE.

## Test

1. Launch KSP and load the affected save.
2. Confirm the Quantum Relay toolbar and UI still work in flight.
3. Visit Space Center and Tracking Station.
4. Return to the main menu.
5. Check `KSP.log` for:
   - `GUI disabled for unsupported scene`
   - `Mission Control disabled for unsupported scene`
   - `Flight bootstrap destroyed`
6. Note whether the grey screen still occurs.

## Suggested commit

```text
fix: isolate relay components during scene transitions
```
