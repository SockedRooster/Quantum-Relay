# Quantum Relay v1.03.1 — standalone part packaging correction

The v1.03 build staged standalone RoosterWorks configs from Distribution, then
overwrote them with legacy +PART[nfex-...] patches from QuantumRelay/Parts.
The published v1.03 GameData asset has SHA256
6bf04141b608e481350e1a14f06743ff54a02019a53347ee6f9872d554de69b2,
matching the inspected incorrect local archive. GitHub asset ID: 633996005.
This confirms the public artifact is affected without relying on assumptions
about which package was uploaded.

## Correction

- Removed the legacy part-copy target and redundant legacy part-config sources.
- Distribution/QuantumRelay/Parts is now the single canonical source for the
  four standalone QR parts. Their models, values, part IDs and animations are
  unchanged from the standalone development assets.
- Packaging rejects legacy part patches, any staged or archived config that
  differs from its canonical source, and missing/changed RoosterWorks models.
- Updated dependency/installation documentation. Public/file versions are
  v1.03.1 and 1.3.1.0; assembly binding identity remains compatible.

No wormhole, CommNet, power, or other gameplay logic changed. The new DLL differs
only in release version metadata/constants. Debdeb/Tuun configs are preserved.

## Verification

Release compilation against installed KSP assemblies: zero warnings/errors.
All 69 existing .NET contract checks passed. Packaging regression checks cover
canonical standalone staging, rejection of old NFE patches, altered config
values, missing models, rejection of the original v1.03 player ZIP, and acceptance
of the corrected archive. Package entries are SHA256-verified against inputs,
and part entries are independently compared with canonical Distribution files.

This is build/package verification, not a new in-game test. The maintainer's
earlier acceptance report applies to the standalone development build. A quick
in-game check of the corrected installed parts remains appropriate.

## Upgrade

Exit KSP and merge the full v1.03.1 GameData/QuantumRelay directory into the game.
All four Parts configs must be updated; replacing only QuantumRelay.dll does
not fix the erroneous part definitions. Keep existing personal Settings.cfg
and customized QuantumBridges.cfg. Part IDs are unchanged. Avoid duplicate
QuantumRelay installations.

The QR models/parts are standalone and do not require Near Future Exploration.
ModuleManager is needed for bundled tech-tree/compatibility patches; Kerbalism
remains optional. The v1.03 tag is preserved rather than rewritten.
