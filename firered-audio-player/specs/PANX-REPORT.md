# Bounded external battle sound panX — 2026-10-11

This adds the original `m4aMPlayPanpotControl` control to the existing C# synthesis core: `SetPlayerPan(player, pan, ushort trackBits = 0xffff)` writes signed external pan only on selected existing tracks and returns their count. Missing players, empty masks, and FINE tracks do not change. Paused tracks retain their state; a fresh Start resets panX. The external value is added to twice the sequence PAN before clamping to [-128,127]. It never overwrites sequence PAN. The same source patch is mirrored in the main game, which retains its previous independent Snorlax mode2 extension.

The original functions are extracted from pinned pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`, `src/m4a.c` lines764 and1314. The test script verifies source SHA-256 values, compiles only those functions with a narrow host struct, and compares2646 volume vectors and112 control vectors. Existing gain math, profile behavior, resource pack and unrelated modulation branches stay as before. ModernDryV1 retains its legacy zero-pan math exactly.

Validation:315 complete audio selftests including53 new pan checks; all eight existing profiles cover PCM and PSG left/right/center, live reversal, independent players, sequence PAN coexistence, mask/FINE/stop/restart behavior and signed-input rejection.32 real-resource zero-pan cases across all eight profiles match this repository's fixed preimage PCM byte-for-byte. The main copy adds one retained Snorlax mode2 regression (33 cases).

The main game's separate actual Godot4.7.2/.NET10.0.12 adapter harness passes25 assertions. It proves token ownership, replacement/reset/cancellation isolation, and actual downstream stereo direction for player/opponent Pound and Tackle (four captured cases). It uses Dummy; no physical audible-listening or hardware-bit-perfect claim is made. No WAV/audio recording outputs are written.

## Existing volume limit exposed by expanded oracle

All production `ExternalVolume` assignments were inspected: initialization64 and private `SetBgmExternalVolume` calls with21 or64 only. This is not an API restriction: the existing `Track.ExternalVolume` field is a public int. Values above64 can expose an existing byte-overflow difference. Exact retained failing vector: PAN=-64, panX=-128, VOL=127, volX=127, velocity=64, rhythmPan=-64. Original C gives(183,0), current C# gives(255,0). The same mismatch exists at panX=0, because PAN=-64 already clamps the coordinate. This patch does not fix or claim parity for that broader public-field behavior. The accepted C table uses external volumes0/21/64; other volume values and modulation types1/2 remain outside this pan patch.

## Reproduce

Use the main project's pinned `scripts/env.sh` (.NET SDK10.0.401), not a PATH fallback. In the audio repository:

```sh
python3 tools/pan-source-oracle.py "$FIRERED_ORIGINAL_REFERENCE" /tmp/firered-pan-oracle
flock /tmp/firered-remake-build.lock dotnet build core/FireRedAudio.Core.csproj -m:1 -nr:false
flock /tmp/firered-remake-build.lock dotnet build src/FireRedAudio.csproj -m:1 -nr:false
PAN_ORACLE=/tmp/firered-pan-oracle/oracle.tsv dotnet src/bin/Debug/net10.0/FireRedAudio.dll selftest resources/firered.pack
```

The paired main test folder supplies the exact fixed-commit zero-PCM comparison and real Godot host runner. Baselines are main0e9438728d17f2e03262dfc4ad4abbb53e9e3e51 and audio38b3b6ef421e3b3e3e3ced5cafa0d505859bdb26. No solution build, GUI changes, new synth, full corpus render, or active-repository commit was performed by this worker.
