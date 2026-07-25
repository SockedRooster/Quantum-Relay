Quantum Relay

**True CommNet Routing Through Wormholes**

Developed by **SockedRooster**  
A **RoosterWorks** project  
Version **1.0.0** - Licensed under the **MIT License**

Quantum Relay extends KSP's stock CommNet graph so communications can route through the paired Promised Worlds wormholes. A valid gateway vessel must be close to a supported anomaly, carry a probe core and CommNet hardware, have an RFL-2000 reflector deployed, and supply ElectricCharge while the link is active.

#Requirements

- Kerbal Space Program 1.12.5
- Promised Worlds
- Near Future Exploration

#Installation

1. Build `Source/QuantumRelay/QuantumRelay.csproj` or use the compiled release DLL.
2. Copy the `QuantumRelay` folder into `Kerbal Space Program/GameData/`.
3. Confirm the final path is `GameData/QuantumRelay/Plugins/QuantumRelay.dll`.

#Gateway rules

- Within 250 km of `KevbasAnomalyA` or `KevbasAnomalyB`
- RFL-2000 reflector antenna present and deployed
- Probe control hardware present
- CommNet capability present
- 5 ElectricCharge per second consumed by each gateway while online

When either reflector retracts, power is insufficient, or a gateway leaves range, the wormhole edge is removed during the stock CommNet rebuild and vessels return to their available local communication paths.

#Configuration

Edit `GameData/QuantumRelay/QuantumRelay.cfg` to change supported wormhole names, gateway radius, scan intervals, and ElectricCharge consumption.

#Support and logs

Search `KSP.log` for `[QuantumRelay]`. Include the relevant log section, KSP version, dependency versions, and reproduction steps when reporting an issue.

#License

Quantum Relay is copyright © 2026 RoosterWorks and is distributed under the MIT License. See `LICENSE`.


## v1.0.2 Interface

The stock toolbar window now shows live link state, both selected gateways, hardware readiness checks, distance to each wormhole endpoint, and electric-charge reserves.
