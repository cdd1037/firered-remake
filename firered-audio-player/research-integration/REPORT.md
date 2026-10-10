# Current playable-route synthesis inventory

## Result

The four active main-game recording manifests contain **143 unique IDs**: **110 song-table cues + 33 normal cries**. The additional known missing recorded cue, **SE_BOO**, makes **144 audited candidates: 111 noncry + 33 cry**. No manifest IDs overlap.

- `small-audio-delivery`: 134 IDs
- `repel-audio`: 1 ID
- `town-route-audio`: 5 IDs
- `route22-cry-audio`: 3 IDs

The manifested categories are **23 BGM, 79 SE, 8 fanfares, 33 normal cries**. “SE” includes the three finite musical player-2 cues `MUS_CAUGHT_INTRO`, `MUS_EVOLUTION_INTRO`, and `MUS_JIGGLYPUFF`; they have no fanfare counter. The source-player distribution is player 0: 23, player 1: 68, player 2: 18, player 3: 1, player 4: 33. `SE_BOO` adds one player-1 SE.

## Actual synthesis, not header-read coverage

The separately compiled .NET 10 research project references `core/FireRedAudio.Core.csproj`; it changes no runtime or game source. It uses the authenticated original pack and `SourceFiltered` profile, rendering each of the 111 noncry candidates for 20 seconds with block sizes 512 and 257. All 25 looping cues then receive an additional 120-second render with both block sizes.

**Passed:** 111/111 noncry cues produce finite nonzero PCM with identical hashes across both block sizes; no exceptions, nonfinite values, silent cues, or clipped samples. All 86 finite cues finish within 20 seconds. All 25 looping cues reach every source-declared looping track's GOTO within 120 seconds. No missing instruction or parameter is encountered in these bounded runs.

The 33 listed normal cries are covered by the existing authenticated 386-species mode-0 catalog assessment (`results/phase5-catalog/coverage.json`); this audit maps their exact species, tone and sample addresses and does not rerun or misreport that whole-species assessment.

Runtime: .NET 10.0.12. Frozen assessment core DLL SHA-256: `81b1a1951fedc8e450ae5704534ef67483c232430886fe4802c07d7850a9fee3`. Synthesis took 105.95 seconds total for 10,440 seconds of generated output. Ongoing host/core integration edits made after the research build are not part of this frozen result; exact baseline source hashes are retained.

## Identity and outstanding host requirements

All manifested source players, ROM song-header priorities and source fanfare counters match the fixed source: `pret/pokefirered` commit `037335f4c725d7c9aecdac87066f2002b4bd7e14`, ROM SHA-1 `41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc`. Noncry song-table entries are checked directly against the ROM and linker map. `looping` comes from source GOTO declarations, never inferred from a 20-second timeout.

- Original player IDs remain essential. The frozen core research routing is player 0 for BGM, 1 for all song-table effects and 2 for cries. These isolated tests do not prove concurrency equivalence for the original SE1/SE2/SE3 lanes or shared voice arbitration.
- Eight fanfares require their independent source counters: level up 80; obtain item 160; evolved 220; obtain TMHM 220; heal 160; obtain badge 340; obtain key item 170; Dex rating 196. Count N source frames plus a separate zero-counter completion frame; preserve BGM pause/resume, cancellation and ownership tokens.
- `SE_LOW_HEALTH` and `SE_M_GUST` are looping effects; the host must cancel them explicitly.
- Normal cries remain mode 0 with authenticated species mapping and fixed normal parameters. Arbitrary cry modes, caller pan variants and global stereo reproduction are outside this assessment.
- `SE_BOO` successfully renders as song 22, header `0x086b5adc`, source player 1, priority 4, finite, no counter. It remains absent from the recorded catalogs. Its four uses are badge-denial blocks imported from `data/scripts/route23.inc` in the Route22 generated scripts.
- Treat this inventory as a finite allowlist. Unknown IDs/modes and unsupported operations must fail explicitly, without guessed mappings or substitute sounds.

This is the selected current-route catalog scope, not a whole-game reachability audit, bit-perfect hardware claim, simultaneous-player acceptance, physical-output check or listening acceptance. Preserve the recorded default while testing the opt-in host.

## Deliverables and reproduction

- `inventory.json`: full 144-candidate machine-readable mapping, identity, source parameters, per-ID render evidence and 33 relevant cry mappings
- `integration-inventory.csv`: compact mapping for review/import
- `summary.json`: exact coverage denominator, loop-track checks, frozen assembly/source hashes, limitations and artifact hashes
- `render/coverage.json`, `loop-render/coverage.json`: measured runs; per-cue event traces alongside them
- `prepare.py`, `IntegrationCoverage.csproj`, `Program.cs`, `verify.py`, `run.sh`: reproducible assessment

Run `./research-integration/run.sh` from the audio project. The verifier rejects empty/missing rows, runtime errors, nonfinite/silent/clipped output, block-size differences, finite cues that do not finish and any unobserved source loop track. A rerun intentionally assesses the then-current core and refreshes output.

A first preparation attempt hit an overly broad static composite-voice recursion and was corrected without changing runtime. The ensuing missing-inventory launch is preserved in `setup-missing-inventory.log`; it is not counted as a synthesis pass. Static composite candidate scans can include unexecuted keys and unsupported nested candidates; they are explicitly separate from actual runtime coverage.
