# Quantum Relay v1.3.1 Test Checklist

## Build and loading

- Run `dotnet clean` and `dotnet build -c Release`.
- Confirm zero compiler errors.
- Replace the installed DLL and all included part/config files.
- Start KSP and confirm no `[QuantumRelay]` exceptions in `KSP.log`.

## Link stability — primary regression test

1. Establish a working bridge.
2. Leave the flight scene running for at least two minutes.
3. Observe at least eight 15-second discovery scans.
4. Confirm the link does not drop, return to synchronizing, or recreate its CommNet line.
5. Confirm screen notifications do not repeatedly announce the same link reconnecting.

## GUI settings

- Confirm global signal-quality is gone.
- Confirm global gateway-power requirement is gone.
- Change activation radius and select **Apply and Save**.
- Confirm the displayed radius updates and a gateway refresh occurs.
- Reload the settings and confirm the saved radius remains.
- Test screen-message, automatic CommNet rebuild, and debug-log toggles.

## Signal levels

Confirm hardware values:

- Legacy reflector-only: 25%
- QR-100: 40%
- QR-250: 60%
- QR-500: 100%
- QR-750: 125%

Confirm weakest-endpoint bridge values, including:

- Legacy ↔ QR-750 = 25%
- QR-100 ↔ QR-750 = 40%
- QR-250 ↔ QR-750 = 60%
- QR-500 ↔ QR-750 = 100%
- QR-750 ↔ QR-750 = 125%

## Power

- Confirm each modern relay reports converter-backed EC draw in Dynamic Battery Storage/System Monitor.
- Confirm standby, synchronization, and operational rates change correctly.
- Confirm there is no additional unexplained gateway power draw.
- Disable and re-enable the relay and confirm power/state transitions remain correct.

## QR-750 Horizon Prime

- Confirm the part appears in the editor.
- Confirm it uses the enlarged giant-reflector geometry.
- Deploy and verify the Horizon Prime startup text.
- Confirm synchronization takes approximately five seconds.
- Confirm hardware signal reads 125%.
- Confirm operational draw reads 6.0 EC/s.
- Save/reload a deployed QR-750 craft and confirm state persistence.

## Save/load and scene changes

- Quicksave with an online link, reload, and confirm no duplicate links.
- Switch to Space Center and back to the vessel.
- Switch vessels near each endpoint.
- Confirm the link remains stable or performs only one legitimate validation after loading.
