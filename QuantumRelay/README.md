# Quantum Relay v1.3.0 Cleanup

This is a full-source replacement, not an overlay patch.

Included fixes and features:

- Restores the complete source tree, including `WormholeScanner.cs` and the `WormholeInfo` type.
- Uses an explicit compile manifest in `QuantumRelay.csproj`, preventing source files from being silently omitted.
- Retains Dynamic Battery Storage / Resource Monitor-compatible stock converter power loads.
- Retains configurable power, multi-wormhole routing, network overview, and tier startup sequences.
- Debounces temporary reflector-state changes before resetting synchronization.
- Applies tier signal strength at the weaker endpoint of each link:
  - QR-100 Pioneer: 40%
  - QR-250 Voyager: 60%
  - QR-500 Event Horizon: 100%

## Install source

1. Back up your current `QuantumRelay` source directory.
2. Delete the contents of that source directory. Do not merge this package over an older alpha tree.
3. Copy every file and folder from this package into the empty source directory.
4. Build from the directory containing `QuantumRelay.csproj`:

```powershell
dotnet clean
dotnet build -c Release
```

5. Replace `GameData\QuantumRelay\Plugins\QuantumRelay.dll` with the compiled DLL.
6. Copy the included `Parts`, `QuantumRelayResources.cfg`, and `MultipleWormholes.cfg` files into `GameData\QuantumRelay`.
7. Test with newly placed relay parts first because old craft files can retain old module values.
