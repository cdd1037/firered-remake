# Development environment and source recovery

The available workspace reverted to local `5722153` after the earlier fixes.
The later local commit objects and temporary verification artifacts were not
present. This recovery does not claim to reproduce the missing commit hashes.

## Source provenance

The preserved branch `preservation/pre-recovery-5722153` retains the starting
snapshot. Its tree matched the parent of GitHub commit
`e858419be7810ef702c2f6476102320c327d2eb2`.
The 15-file remote delta was restored with individual blob SHA and mode checks.
Local recovery commit `024e07ec5aa493a3036f5a0ded6589e03fff64fb` has the exact
remote tree `9f0bfb13531163e07378378c12757e08b3d4d64e`.

Subsequent shared field-script, movement, dialogue-input, progressive battle-text,
menu-return and Windows export configuration changes were reconstructed from
retained implementation records and checked against the pinned original source.
They require fresh verification, not reuse of missing historical test logs.

## Restored tools

Official Godot 4.7.2 .NET and .NET SDK 10.0.401 / runtime 10.0.12 were restored
outside the project and checked against the toolchain manifest hashes.
`python dev doctor` passed. The preinstalled Godot 4.6.3 is not the project
baseline; `source scripts/env.sh` selects the restored tools.

NuGet restore completed using the official Godot package directory and
nuget.org. Export templates were not restored because this task does not export
a game; a future export must install the matching official templates first.

## Fresh verification

- Full `FireRed.Current.slnx` serial build: zero warnings and errors.
- Shared field-script public-flow and primitive regressions: 29 passed.
- Known solid interaction-tile collision regressions: 17 passed.
- Combined text/input/menu and neighboring presentation regressions: 186 passed.
- Windows export configuration contracts: eight passed.
- Controlled actual `CampaignView` battle consumer probe: final-assembly
  headless and rendered passes, covering progressive reveal, held/released input, semantic ACK
  separation, visual continuation and two-stage level-up stats.

The final production `Game.Godot.dll` used by that controlled probe had SHA256
`2f06651f4bead53a10f30d324e20e4688772f39d8dd74665805dc774d4258d90`.
The probe uses a fresh, validated synthetic fixture and real battle commands,
not a natural full-story walkthrough.

The first attempted Application filter used a binary built before the new test
file was discovered and failed with no matching tests. It was not counted as a
pass; the final rebuild and subsequent complete filtered runs replaced it.
Ignored field-return and fog reference fixtures were regenerated through the
maintained independent source extractors (6 traces / 146 rows and 19 traces /
627 rows). They are not committed generated artifacts.

This is focused verification, not a full-suite or complete-game acceptance.
Windows execution, real audio output and late-game checkpoint-dependent paths
are not established by these results. No new playable export, ZIP or Drive
upload was produced.

## Additional issue found and repaired

The native smoke encountered an existing collision fault when walking west from
the upstairs bedroom's `(7,4)` into the solid television at `(6,4)`.
The original baseline and reconstructed tree had the same movement-rule blob;
this was not a regression in the restored Mom or television dialogue scripts.
The user requested that encountered problems also be fixed.

The shared collision path now blocks the explicitly defined source interaction
types `0x80..0xa3` only when the target is solid. Unknown types, unsupported
passable cells, malformed cells, door dispatch and ledge precedence retain their
checks. Domain tests cover the 36 named types and strict guard cases; five added
public Application cases cover bedroom television, bookshelf and dresser
collisions followed by retreat. The complete build, 17 Domain, 29 Application,
186 Presentation and eight export-contract cases passed after this correction.
The generated-map inventory audit is not a claim of natural traversal of every
map. See [shared field-script primitives](shared-field-script-primitives.md).
