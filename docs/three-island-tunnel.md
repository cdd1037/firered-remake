# Three Isle Path / Dunsparce collection

This slice mounts the original Three Island tunnel and the eastern Port grass. It does not change fonts, audio, distribution policy, or the National Dex prerequisites.

## Source behavior

Pinned reference: pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

- `data/maps/ThreeIsland_DunsparceTunnel/scripts.inc`: transition checks `IsNationalPokedexEnabled`; before the Dex, the prospector faces the wall at (6,3); afterward `setmaplayoutindex` selects the dug-out layout. Both original Port warps remain unchanged.
- The prospector gives one Nugget. Full bag leaves the receipt flag unset, so talking again can retry. The hidden Nugget has its own source flag and remains separate.
- `src/data/wild_encounters.json`: eastern Port land slots contain Dunsparce. Access is constrained by actual source geometry, not a second encounter-region allowlist.
- `src/overworld.c::UpdateEscapeWarp`: outdoor-to-indoor entry records the entrance X and Y plus one unless facing south. `allow_escaping` governs Rope use; the tunnel returns to its actual western or eastern entrance.
- `src/region_map.c::GetPlayerPositionOnRegionMap`: underground maps project through the saved escape entrance. Three Isle Path's section coordinates are source placeholders and are not shown as a fabricated player marker.

## Shared implementation boundaries

- `setmaplayoutindex` is a shared generated-IR instruction and application effect. Alternate layouts preserve map ID, event header and warp definitions.
- Field movement selects destination layout before evaluating an actual warp's arrival cell and facing. It does not scan transition scripts on ordinary steps.
- Active layout is runtime-only. Restore/save validation replays generated transition IR in an isolated interpreter; it never reads raw source or changes saved flags, actors, rewards or RNG.
- First-Sevii land tables are loaded from converted encounter headers. Dunsparce enters the existing canonical ownership, growth, machine/tutor and breeding systems.
- Map-number extension uses actual key sets, source identities and unchanged existing values, rather than historical predecessor counts.
- Shared items do not identify their giver: the Bridge-specific Nugget handling checks its actual source caller.
- Additional collection presentation is a shared species-keyed catalog. Existing sprite data is reused where pixels match; missing icon/footprint/capture metadata are generated from the pinned source.

## Verification boundary

Targeted tests use controlled current-checkpoint starting positions, then public movement, interaction, item and battle commands. The pre-National-Dex branch is a controlled gate fixture, not a claim that the entire original story was replayed naturally. Post-Dex traversal, rewards, capture and Escape Rope must round-trip the current save validator. Both Seafoam stopped-current layouts are regression cases for the shared layout seam.

Import commands and exact source/atlas regeneration checks are documented in `resource-reconstruction-guide.md`. Physical audio, a full natural new-game-to-postgame playthrough, and native-window visual validation remain separate gates.

The prospector uses the existing shared field-item service. Its entry/event, reachable
programs and item symbols are imported into the ordinary service catalog; the runtime
adapter retains source receipt flags and bag-full branches. Actor identity remains
stable when a source transition moves its template. The same binding decoder is used
by the existing TM acquisition services.

### Targeted application result (2026-10-16 UTC)

Actual toolchain: .NET SDK `10.0.401`, MSBuild `18.9.11`, host/runtime
`10.0.12` on Linux x64. From the repository root:

```sh
source scripts/env.sh

dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj \
  --no-restore \
  --filter FullyQualifiedName~DunsparceTunnelTests \
  --logger 'trx;LogFileName=dunsparce-tunnel-final.trx' \
  --results-directory .toolhome/test-results/dunsparce-tunnel
```

Result: **10 passed, 0 failed, 0 skipped**, with 13 seconds of test execution.
The preserved final artifacts are
`.toolhome/test-results/dunsparce-tunnel/dunsparce-tunnel-final.trx` and
`.toolhome/test-results/dunsparce-tunnel/final.log`.

The cases cover:

- The pre-Dex prospector template, blocked corridor, original dialogue and western exit.
- Post-Dex public traversal in both directions, including eastern re-entry, and
  Continue while standing on a tile blocked by the base layout.
- A genuinely full item pocket, public Bag toss, successful prospector retry,
  receipt persistence and duplicate prevention.
- The independent hidden Nugget, repeated-pickup rejection, and both Nuggets
  surviving strict save/Continue together.
- Escape Rope after Continue from both entrances, consuming exactly one Rope and
  returning to western `(16,5)` or eastern `(38,6)`; public Town Map open/close and
  the corresponding source atlas projections `(18,13)` and `(19,13)`.
- Both Seafoam stopped-current layout restores with consistent boulder flags and
  metatile edits, preserving the serialized state and RNG.
- Actual Port grass `turn` and `move` commands, the exact rate-1 source table,
  Dunsparce capture, identity/IV retention, Port met location and strict save/Continue.

Starting positions, supplies and encounter RNG/modifiers are controlled fixtures.
The pre-Dex branch deliberately changes the three-part Dex gate in a recorded
late-game state and bypasses restore only for that branch; it does not claim a
valid pre-Dex historical save or natural story replay. All post-Dex cases use
validated current saves. Atlas projection checks do not replace a native-window
rendering check.

Earlier failing runs remain in the same artifact directory, including
`dunsparce-tunnel.trx`, `dunsparce-tunnel-second.trx`,
`dunsparce-tunnel-third.trx`, `dunsparce-tunnel-field.trx`,
`dunsparce-tunnel-complete.trx` and `dunsparce-tunnel-services.trx`.
They record the missing hidden-item constant, initial fixture object-pool error,
shared Nugget handler and battle admission failures, strict JSON schema mismatch,
and the obsolete global Nugget save bound. None was relabeled as a pass; the
passing rerun uses its own final artifact.

### Default solution regression

After the application/content/Godot builds, the final stable-resource run was:

```sh
source scripts/env.sh
dotnet test FireRed.Current.slnx --no-build --no-restore -m:1 \
  --logger 'trx;LogFilePrefix=dunsparce-final' \
  --results-directory .toolhome/test-results/dunsparce-full-final
```

Result: **2410 passed, 0 failed, 0 skipped** across Application 282, Battle 1605,
Content 232, Domain 244, Presentation 19, Process 1 and ResourceImport 27.
Godot build also passed with zero warnings/errors. Default solution tests exclude
opt-in audio tests; these results do not claim actual audio-output verification.

The first full run is retained under `.toolhome/test-results/dunsparce-full`:
2409 passed and one Content failure identified an incorrect shared-trainer output
root in the first-Sevii rules importer. The importer now uses the generated-tree
root; an isolated-output regression checks that no nested shared catalog is
created and `--check` uses the canonical catalog without writing. The accidental
catalog was preserved outside the runtime tree before the final run.

A subsequent hot-path review replaced whole-catalog dictionary copies in layout
selection with a lightweight read-only single-entry overlay. This change was
verified with the same targeted application command, using
`dunsparce-tunnel-overlay.trx`: **10 passed, 0 failed, 0 skipped**. The full suite
above predates only this allocation change; it was not redundantly rerun.
