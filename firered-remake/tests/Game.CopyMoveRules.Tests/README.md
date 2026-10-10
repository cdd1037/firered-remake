# Source Mimic and Sketch

Independent candidate against `71085ee`; [source contract](SOURCE_CONTRACT.md) records the exact pinned command, history, PP, copy-lifetime and presentation policy.

Focused result: **16 groups, 392 assertions, 14 complete turns, zero failures** on hosted .NET **10.0.12**, compiled with official SDK **10.0.401**. The separate read-only source probe passes **25 contracts**, authenticating six reference files against commit `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

Coverage:

- 352/354 authenticated current singles recipes; only Focus Punch and Snatch remain unregistered. Genuine Metronome/Assist pools are unchanged, and supplied Metronome cannot invoke the excluded copies.
- Both actor sides complete real wild turns, spend subsequent copied PP, and retain the original slot's PP Ups. Mimic's temporary live slots remain separate from permanent moves/PP; Sketch writes the source permanent slots.
- Exact independent last-used/last-printed history, source exclusions, duplicate failure, Substitute, Protect, semi-invulnerability and Lock-On. No ordinary copy accuracy RNG.
- Genuine Sleep Talk invokes both copy commands with its real selected result slot and only one caller PP charge.
- Ordinary switch, actual faint replacement, escape, capture delivery, Transform in both directions and permanent move learning.
- The uncommon source all-slot Sketch write with an existing Mimic flag, including its subsequent independent live/party PP.
- Atomic malformed overlay rejection, legacy contract/entry rejection, battle diagnostic JSON and typed event serialization.
- Source-authenticated success strings through the actual read-only presentation model and `PretBattleText.Pages`.
- Public Domain permanent party output projected into a normal Application field state, standard `SaveCodec` validation and a fresh `CampaignSession.Restore`. This is a controlled projection/Continue check, not a reflective live session or natural played encounter.

The only product Application changes update event-time display slots. Existing wild and trainer party owners remain the permanent mutation boundary. No persistent owned/save schema fields, dependencies or Metronome selection changes were added.

## Reproduce

Set `FIRERED_TOOLCHAIN_ROOT` to the already installed official toolchain, set `DOTNET_ROOT` to its `dotnet` directory, and run from the candidate or integrated project:

```sh
flock /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" build tests/Game.CopyMoveRules.Tests -m:1 -nr:false
"$DOTNET_ROOT/dotnet" tests/Game.CopyMoveRules.Tests/bin/Debug/net10.0/Game.CopyMoveRules.Tests.dll REFERENCE_CONTENT_PROJECT_ROOT NEW_OUTPUT_DIRECTORY
python3 tests/Game.CopyMoveRules.Tests/source_probe.py PINNED_PRET_SOURCE_ROOT NEW_OUTPUT_DIRECTORY/source-contract.json
```

The runtime runs outside the build lock. The test rejects a preexisting evidence directory and fails on an empty suite or missing content. It reads existing private content inputs without copying them into the candidate.

The original candidate records are retained under `evidence/candidate-710/`. Main integration reran the same 16 groups / 392 assertions / 14 turns at `evidence/integrated-5a5/`, including a new 25-check source probe; `evidence/integrated-build.log` records the successful main build. `evidence/candidate-710/fixture-errors.log` retains four fixture failures: unsupported Master Ball entry, reference equality of immutable move arrays, and use of the action-only validator at the genuine replacement pause. Corrections use the already admitted guaranteed Great Ball scenario, canonical value comparison and the complete owner validator. No source expectation or golden was rewritten.

No solution-wide build, long random/history regression, native window, audio or complete-game acceptance is claimed. The two existing `Game.NestedMoveCallers.Tests` admission assertions are updated to the new current registry count/list; that historical suite was not rerun.
